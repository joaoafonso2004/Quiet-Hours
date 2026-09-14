using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Pungent.Interaction;
using Pungent.Narrative;
using Pungent.NPC;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pungent.EditorTools
{
    /// <summary>
    /// A passagem de verificacao. Percorre as cenas abertas e escreve na consola
    /// tudo o que esta mal. **Nao corrige nada.**
    ///
    /// ---
    ///
    /// **Porque e que isto existe.** Dos 25 bugs da passagem de 11-08, quatro em
    /// cada cinco eram dados da cena e nao codigo, e **nenhum deles dava erro**. Um
    /// movel sem malha, um passo preso a um acontecimento sem dono, uma ancora fora
    /// de sitio — o Unity nao se queixa de nenhuma destas coisas. Sao descobertas a
    /// jogar, semanas depois, por acaso.
    ///
    /// ---
    ///
    /// **A licao da primeira versao, que vale mais do que a ferramenta.**
    ///
    /// A primeira versao disto reportou 547 problemas, e quatro das suas seis
    /// categorias eram mentira. Nao por estar mal escrita: por eu lhe ter dado as
    /// **minhas** invariantes em vez das deste projecto. Este codigo:
    ///
    /// - **deixa campos a nulo de proposito** e resolve-os no arranque — 198 sitios
    ///   fazem `if (x == null) x = FindObjectOfType&lt;T&gt;()`, e nove componentes
    ///   implementam <see cref="IRebindable"/>. Um nulo aqui nao e um bug, e o
    ///   desenho;
    /// - **levanta acontecimentos a partir de campos serializados** — `Notify(campo)`
    ///   e nao `Notify("literal")`. Procurar so literais no codigo nao ve os donos;
    /// - **poe os esconderijos dentro dos moveis**, que e o que um esconderijo e;
    /// - chama `PlayerRoot` aquilo a que os documentos chamam `PLAYER`.
    ///
    /// Por isso esta versao le as convencoes do projecto em vez de as presumir. Uma
    /// ferramenta que grita por tudo deixa de ser lida a terceira vez, e a partir
    /// dai e pior do que nao existir.
    ///
    /// **Quando duvida, cala-se.** Prefere nao reportar um problema verdadeiro a
    /// reportar dez falsos — a unica coisa que esta ferramenta tem para dar e ser
    /// acreditada.
    ///
    /// ---
    ///
    /// **Nao substitui jogar.** So ve o que esta parado numa cena. Nao ve
    /// temporizadores, nao ve ordem de arranque, nao ve nada que so exista em Play.
    /// O que nao verifica esta dito no fim do relatorio, por palavras.
    /// </summary>
    internal static class VerifyEverything
    {
        private const string MenuPath = "Pungent/Verificar tudo";
        private const string MenuPathVerbose = "Pungent/Verificar tudo (detalhado)";

        /// <summary>
        /// Componentes em que um campo a nulo ja bloqueou o jogo. Um nulo aqui e
        /// erro; noutro sitio qualquer e ruido. **Esta lista cresce com as
        /// cicatrizes** — quando um nulo custar uma sessao, o nome vem para aqui.
        ///
        /// **O `HomeTaskInteractable` esteve aqui e saiu.** Os seus campos por
        /// preencher sao `startClip`, `loopClip`, `audioSource`, `effect` e
        /// `progressVisual` — som e visual que ainda nao existem e que o `NEEDS.md`
        /// ja regista como em falta. Sao divida conhecida, nao ligacao partida, e
        /// sozinhos faziam 60 dos 81 erros da primeira execucao util. A correccao a
        /// serio e marcar esses campos com "Vazio =" no tooltip; ate la, ficam no
        /// modo detalhado.
        /// </summary>
        private static readonly HashSet<string> CriticalComponents = new HashSet<string>(StringComparer.Ordinal)
        {
            "ChapterEventRaiser", "ChapterSceneLoader", "HomeTaskZone",
            "StagedHomeTask", "InvasionSigns", "HouseChange", "WitnessedCue", "AttentionSnap",
            "NothingHappened", "HidingSpot", "OverheardCall", "DoorDragInteractable",
        };

        /// <summary>Marcas de campo opcional, lidas do tooltip. Convencao ja existente.</summary>
        private static readonly string[] OptionalMarkers =
        {
            "vazio =", "vazio=", "opcional", "optional", "deixar vazio", "vazio para",
        };

        /// <summary>
        /// Nomes de campo que **esperam** por um acontecimento em vez de o levantarem.
        /// Um campo destes com um id la dentro nao faz dele dono de nada.
        /// </summary>
        private static readonly string[] GateFieldMarkers =
        {
            "requires", "silenced", "applieson", "startson", "decideon", "waits",
            "until", "blockedby", "gate", "needs", "onevent", "after",
        };

        /// <summary>Raizes reais do apartamento, tiradas da cena e nao dos documentos.</summary>
        private static readonly string[] ApartmentRoots =
        {
            "PlayerRoot", "GAME_SYSTEMS", "APARTMENT_SYSTEMS", "ART_PASS_V2",
            "HOME_TASKS", "DREAD_PASS", "CLIMAX_DAY5",
        };

        private enum Severity { Erro, Aviso, Info }

        private sealed class Finding
        {
            public Severity Level;
            public string Check;
            public string Message;
            public UnityEngine.Object Context;
        }

        private static readonly Dictionary<string, string> SourceCache = new Dictionary<string, string>(StringComparer.Ordinal);

        // ----------------------------------------------------------------------------

        [MenuItem(MenuPath, false, 0)]
        private static void Run() => Execute(false);

        [MenuItem(MenuPathVerbose, false, 1)]
        private static void RunVerbose() => Execute(true);

        private static void Execute(bool verbose)
        {
            SourceCache.Clear();

            var findings = new List<Finding>();
            var notChecked = new List<string>();

            var objects = AllObjects();
            if (objects.Count == 0)
            {
                Debug.LogError("[Verificar] Nenhuma cena carregada. Abre a cena antes de correr isto.");
                return;
            }

            CheckDuplicateOwners(objects, findings);
            CheckCueHasTarget(objects, findings);
            CheckMissingScripts(objects, findings);
            CheckFurnitureMeshes(objects, findings);
            int quietNulls = CheckNullSerializedFields(objects, findings, verbose);
            CheckChapterEvents(objects, findings, verbose);
            CheckInteractRaisers(objects, findings);
            CheckCompetingInteractables(objects, findings);
            CheckRoutineAnchors(objects, findings);
            CheckToolRoots(objects, findings);

            notChecked.Add("**Esconderijos e destinos livres de geometria.** Tentei e estava errado: "
                         + "um esconderijo esta dentro de um movel por definicao. Distinguir "
                         + "'debaixo da cama' de 'dentro da parede' precisa de saber qual e o movel "
                         + "de cada sitio, e isso ninguem declara. Continua por verificar.");
            notChecked.Add("**Acontecimentos levantados por indireccao.** Vejo `Notify(campo)`, mas nao "
                         + "vejo `Notify(Escolher())` — e assim que o `EndingSelector` levanta os cinco "
                         + "finais. Por isso os cinco `CH_Epilogue_*` aparecem como 'capitulo noutra "
                         + "cena' e nao estao partidos. Um 'capitulo noutra cena' quer dizer *confirma*, "
                         + "e nao *esta mal*.");
            notChecked.Add("**Valores das ferramentas contra os da cena.** Nenhuma ferramenta declara "
                         + "o que escreveu, por isso nao ha contra o que comparar. Ja divergiu tres vezes.");
            notChecked.Add("**Tudo o que so existe em Play:** temporizadores, ordem de arranque, "
                         + "interpolacoes que nao chegam ao destino, campos resolvidos no Awake.");

            if (quietNulls > 0 && !verbose)
                notChecked.Add(quietNulls + " campos a nulo em componentes nao criticos, calados de "
                             + "proposito. `" + MenuPathVerbose + "` mostra-os.");

            Report(findings, notChecked, objects.Count);
        }

        /// <summary>
        /// Componentes de que so pode haver **um**. Este jogo e uma linha de historia
        /// so, do inicio ao fim: nada aqui e para existir duas vezes.
        /// </summary>
        private static readonly string[] SingleOwnerComponents =
        {
            "ChapterDirector", "NarrativeBlackboard", "PlayerThoughtDirector", "ScreenFade",
            "ScreenEyelid", "HidingMemory", "PhoneMessageService", "GameSystemsRoot",
            "ChapterCard", "HomeTaskDirector", "EndingSelector", "EpilogueStage",
            "ClimaxStage", "PrologueStage", "DayTwoDirector", "DayThreeStage", "DayOneStage",
            "PlayerInteractor", "CameraPhysics", "PrototypeHUD",
        };

        // ----------------------------------------------------------------------------
        // 0. Dois donos do mesmo sistema. **A verificacao mais valiosa deste ficheiro**,
        //    e foi a ultima a ser escrita — o que diz alguma coisa.
        //
        //    O prologo trancava e ninguem percebia porque: havia **dois
        //    `ChapterDirector`**, os dois ligados ao mesmo HUD, e so um deles recebia
        //    acontecimentos. O `Notify` ia todo para o do `GAME_SYSTEMS` pelo
        //    `ChapterDirector.Resolve`; o do `PlayerRoot` ficava parado no passo em que
        //    estivesse e continuava a escrever o objectivo dele no ecra. O jogador via
        //    um objectivo que nao podia avancar, enquanto o jogo real avancava por
        //    baixo. **Nao havia erro nenhum na consola.**
        //
        //    Havia mais dois pelo mesmo caminho: `ScreenEyelid` e `DayTwoDirector`.
        //
        //    Isto tambem apanha o caso ao contrario — uma ferramenta que devia
        //    reconstruir e afinal acrescentou.
        // ----------------------------------------------------------------------------

        private static void CheckDuplicateOwners(List<GameObject> objects, List<Finding> into)
        {
            var seen = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var go in objects)
            {
                foreach (var behaviour in go.GetComponents<MonoBehaviour>())
                {
                    if (behaviour == null) continue;

                    string name = behaviour.GetType().Name;
                    if (Array.IndexOf(SingleOwnerComponents, name) < 0) continue;

                    if (!seen.TryGetValue(name, out var where))
                    {
                        where = new List<string>();
                        seen[name] = where;
                    }
                    where.Add(Path(go));
                }
            }

            foreach (var pair in seen)
            {
                if (pair.Value.Count < 2) continue;

                Add(into, Severity.Erro, "dois donos do mesmo sistema",
                    pair.Key + " existe " + pair.Value.Count + " vezes: " + string.Join(" | ", pair.Value)
                    + ". So um recebe acontecimentos; o outro fica vivo e a mentir.", null);
            }
        }

        // ----------------------------------------------------------------------------
        // 1. Scripts em falta. Barato de apanhar, caro de nao ver: um componente com o
        //    script perdido nao da erro ate alguem reparar que aquilo nao faz nada.
        // ----------------------------------------------------------------------------

        /// <summary>
        /// Um `WitnessedCue` sem alvo nenhum. Os quatro campos alternativos sao
        /// calados um a um pelo <see cref="IsUnusedByKind"/>; esta e a pergunta que
        /// fica de pe — **algum deles esta preenchido?** Nenhum quer dizer um beat que
        /// nunca acontece, calado.
        /// </summary>
        private static void CheckCueHasTarget(List<GameObject> objects, List<Finding> into)
        {
            foreach (var go in objects)
            {
                foreach (var behaviour in go.GetComponents<MonoBehaviour>())
                {
                    if (behaviour == null || behaviour.GetType().Name != "WitnessedCue") continue;

                    var serialized = new SerializedObject(behaviour);
                    bool any = false;
                    foreach (var field in new[] { "lamp", "door", "fridge", "target" })
                    {
                        var property = serialized.FindProperty(field);
                        if (property != null && property.objectReferenceValue != null) { any = true; break; }
                    }
                    if (any) continue;

                    Add(into, Severity.Erro, "cue sem alvo",
                        Path(go) + " e um WitnessedCue sem `lamp`, `door`, `fridge` nem `target`. "
                        + "Nao ha nada para mudar: o beat nunca acontece.", go);
                }
            }
        }

        private static void CheckMissingScripts(List<GameObject> objects, List<Finding> into)
        {
            foreach (var go in objects)
            {
                var components = go.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] != null) continue;
                    Add(into, Severity.Erro, "script em falta",
                        Path(go) + " tem um componente com o script perdido (indice " + i + ").", go);
                }
            }
        }

        // ----------------------------------------------------------------------------
        // 2. Moveis sem malha. Ja aconteceu duas vezes, treze moveis de cada vez, e
        //    levou tres `HouseChange` com eles — um objecto sem malha continua a
        //    existir, continua a rodar quando lhe mandam, e nao se ve.
        // ----------------------------------------------------------------------------

        private static void CheckFurnitureMeshes(List<GameObject> objects, List<Finding> into)
        {
            var artPass = objects.FirstOrDefault(o => o.name == "ART_PASS_V2");
            if (artPass == null) return;

            foreach (var filter in artPass.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null) continue;
                Add(into, Severity.Erro, "movel sem malha",
                    Path(filter.gameObject) + " tem MeshFilter sem malha.", filter.gameObject);
            }

            foreach (var renderer in artPass.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                if (materials != null && materials.Length > 0 && materials.All(m => m != null)) continue;
                Add(into, Severity.Aviso, "movel sem material",
                    Path(renderer.gameObject) + " tem um slot de material vazio.", renderer.gameObject);
            }
        }

        // ----------------------------------------------------------------------------
        // 3. Campos a nulo — **so os que ninguem vai preencher sozinho.**
        //
        //    Tres maneiras de um nulo ser de proposito neste projecto, e todas se leem
        //    do codigo em vez de se presumirem:
        //      a) o tooltip diz "Vazio =" ou "Opcional";
        //      b) o componente implementa `IRebindable` e resolve-se no arranque;
        //      c) o proprio ficheiro do componente faz `if (x == null) x = Find...`.
        // ----------------------------------------------------------------------------

        private static int CheckNullSerializedFields(List<GameObject> objects, List<Finding> into, bool verbose)
        {
            int quiet = 0;

            foreach (var go in objects)
            {
                foreach (var behaviour in go.GetComponents<MonoBehaviour>())
                {
                    if (behaviour == null) continue;

                    var type = behaviour.GetType();
                    if (type.Namespace == null || !type.Namespace.StartsWith("Pungent", StringComparison.Ordinal))
                        continue;
                    if (typeof(IRebindable).IsAssignableFrom(type)) continue;

                    var serialized = new SerializedObject(behaviour);
                    var property = serialized.GetIterator();

                    bool enterChildren = true;
                    while (property.NextVisible(enterChildren))
                    {
                        enterChildren = false;

                        if (property.propertyPath == "m_Script") continue;
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        if (property.objectReferenceValue != null) continue;

                        string fieldName = property.propertyPath.Split('.')[0];
                        if (IsMarkedOptional(type, fieldName)) continue;
                        if (SelfResolves(type, fieldName)) continue;
                        if (IsUnusedByKind(behaviour, type.Name, fieldName)) continue;

                        bool critical = CriticalComponents.Contains(type.Name);
                        if (!critical && !verbose) { quiet++; continue; }

                        Add(into, critical ? Severity.Erro : Severity.Aviso, "campo a nulo",
                            Path(go) + " / " + type.Name + "." + property.propertyPath + " esta a nulo.", go);
                    }
                }
            }

            return quiet;
        }

        /// <summary>
        /// O campo nao e usado pelo **tipo** que este componente esta a fazer?
        ///
        /// ---
        ///
        /// **Vinte e tres dos quarenta e oito erros eram isto**, e escondiam quatro
        /// que eram a serio.
        ///
        /// Um <see cref="WitnessedCue"/> tem quatro alvos possiveis — `lamp`, `door`,
        /// `fridge`, `sightTarget` — e usa **um**, conforme o `effect`. Os outros tres
        /// estao a nulo de propósito, sempre, em todos os que existem. Reporta-los era
        /// garantir que ninguem lia a lista.
        ///
        /// Uma <see cref="HouseChange"/> so usa a `lamp` quando o `kind` e `Light`.
        /// **E quando e `Light`, uma `lamp` a nulo e um bug a serio** — foi assim que
        /// a luz do quarto do Rui deixou de acender na noite das 02:47, sem erro
        /// nenhum. Por isso este metodo le o tipo em vez de calar o campo: cala-o
        /// quando nao serve, e deixa-o gritar quando serve.
        /// </summary>
        private static bool IsUnusedByKind(MonoBehaviour behaviour, string typeName, string fieldName)
        {
            var serialized = new SerializedObject(behaviour);

            if (typeName == "WitnessedCue")
            {
                // Os alvos alternativos nunca se reportam um a um. Quem verifica que
                // ha pelo menos um e o `CheckCueHasTarget`.
                return fieldName == "lamp" || fieldName == "door"
                    || fieldName == "fridge" || fieldName == "sightTarget";
            }

            if (typeName == "HouseChange" && fieldName == "lamp")
            {
                var kind = serialized.FindProperty("kind");
                if (kind == null) return false;
                if (kind.enumValueIndex < 0 || kind.enumValueIndex >= kind.enumNames.Length) return false;
                return kind.enumNames[kind.enumValueIndex] != "Light";
            }

            return false;
        }

        private static bool IsMarkedOptional(Type type, string fieldName)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var field = current.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field == null) continue;

                var tooltip = field.GetCustomAttribute<TooltipAttribute>();
                if (tooltip == null || string.IsNullOrEmpty(tooltip.tooltip)) return false;

                string lower = tooltip.tooltip.ToLowerInvariant();
                return OptionalMarkers.Any(marker => lower.Contains(marker));
            }

            return false;
        }

        /// <summary>
        /// O componente preenche este campo sozinho? Le-se do ficheiro dele. E feio,
        /// e e mais barato do que anotar 198 campos a mao.
        /// </summary>
        private static bool SelfResolves(Type type, string fieldName)
        {
            string source = SourceFor(type);
            if (string.IsNullOrEmpty(source)) return false;

            string escaped = Regex.Escape(fieldName);
            return Regex.IsMatch(source, escaped + @"\s*=\s*(FindObjectOfType|FindAnyObjectByType|FindFirstObjectByType|GetComponent|Resolve)")
                || Regex.IsMatch(source, @"if\s*\(\s*" + escaped + @"\s*==\s*null\s*\)");
        }

        private static string SourceFor(Type type)
        {
            if (SourceCache.TryGetValue(type.Name, out var cached)) return cached;

            string text = string.Empty;
            string root = System.IO.Path.Combine(Application.dataPath, "Pungent", "Scripts");
            if (Directory.Exists(root))
            {
                var file = Directory.GetFiles(root, type.Name + ".cs", SearchOption.AllDirectories).FirstOrDefault();
                if (file != null) text = File.ReadAllText(file);
            }

            SourceCache[type.Name] = text;
            return text;
        }

        // ----------------------------------------------------------------------------
        // 4. Acontecimentos sem dono. "Prender um passo a um evento sem dono bloqueia
        //    tudo o que vem a seguir e nao da erro. Aconteceu seis vezes."
        //
        //    Um dono e: um `ChapterEventRaiser`, um `RaiseOnComplete`, um
        //    `Notify("literal")` no codigo, ou **um campo de texto serializado que nao
        //    seja um portao** — que e como a maior parte deste jogo levanta as coisas.
        //
        //    Um capitulo em que *nenhum* acontecimento tem dono nao esta partido: esta
        //    noutra cena. Isso e um aviso, e um so.
        // ----------------------------------------------------------------------------

        private static void CheckChapterEvents(List<GameObject> objects, List<Finding> into, bool verbose)
        {
            var chapters = LoadChapters();
            if (chapters.Count == 0) return;

            var owners = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var raised in ScanCodeRaisedEvents())
                Own(owners, raised, "codigo");

            foreach (var chapter in chapters)
            {
                for (int i = 0; i < chapter.StepCount; i++)
                {
                    var step = chapter.StepAt(i);
                    if (step?.RaiseOnComplete == null) continue;
                    foreach (var raised in step.RaiseOnComplete)
                        if (!string.IsNullOrWhiteSpace(raised))
                            Own(owners, raised, "CH:" + chapter.name);
                }
            }

            foreach (var go in objects)
            {
                foreach (var behaviour in go.GetComponents<MonoBehaviour>())
                {
                    if (behaviour == null) continue;
                    var type = behaviour.GetType();
                    if (type.Namespace == null || !type.Namespace.StartsWith("Pungent", StringComparison.Ordinal))
                        continue;

                    foreach (var pair in SerializedEventStrings(behaviour))
                        Own(owners, pair, Path(go));
                }
            }

            foreach (var chapter in chapters)
            {
                var waited = new List<KeyValuePair<string, string>>();

                if (!string.IsNullOrWhiteSpace(chapter.StartsOnEvent))
                    waited.Add(new KeyValuePair<string, string>(chapter.StartsOnEvent, chapter.name + " comeca em"));

                for (int i = 0; i < chapter.StepCount; i++)
                {
                    var step = chapter.StepAt(i);
                    if (step == null) continue;

                    if (step.Ends != ChapterDefinition.Completion.Event) continue;

                    if (string.IsNullOrWhiteSpace(step.EventId))
                    {
                        Add(into, Severity.Erro, "passo sem acontecimento",
                            chapter.name + " passo " + i + " (\"" + step.Objective + "\") acaba por "
                            + "acontecimento mas nao diz qual. Fica preso para sempre.", chapter);
                        continue;
                    }

                    waited.Add(new KeyValuePair<string, string>(step.EventId,
                        chapter.name + " passo " + i + " (\"" + step.Objective + "\") espera por"));
                }

                if (waited.Count == 0) continue;

                var orphans = waited.Where(w => !owners.ContainsKey(w.Key)).ToList();
                if (orphans.Count == 0) continue;

                if (orphans.Count == waited.Count)
                {
                    Add(into, Severity.Aviso, "capitulo noutra cena",
                        chapter.name + ": nenhum dos seus " + waited.Count + " acontecimentos tem dono "
                        + "nesta cena. Provavelmente pertence a outra. Abre-a e volta a correr, ou "
                        + "confirma que o capitulo ainda faz parte do jogo.", chapter);
                    continue;
                }

                foreach (var orphan in orphans)
                    Add(into, Severity.Erro, "acontecimento sem dono",
                        orphan.Value + " '" + orphan.Key + "', e ninguem o levanta. O capitulo para "
                        + "aqui e nao da erro.", chapter);
            }

            foreach (var pair in owners)
            {
                if (pair.Value.Count < 2) continue;

                // Varios epilogos a levantarem `game_end` sao ramos que se excluem uns
                // aos outros, e nao dois donos do mesmo momento.
                if (pair.Value.All(v => v.StartsWith("CH:", StringComparison.Ordinal))) continue;

                // **Isto nao chega para ser um aviso.** Um passo que levanta o
                // acontecimento ao terminar e um componente que guarda o mesmo id num
                // campo sao o padrao normal deste jogo — nao consigo distinguir daqui
                // quem levanta de quem espera. Reportado como aviso, dava 29 alarmes
                // falsos e a ferramenta deixava de ser lida. Fica para o modo detalhado,
                // como material de leitura e nao como problema.
                if (!verbose) continue;

                Add(into, Severity.Info, "dois donos (por confirmar)",
                    "'" + pair.Key + "' e levantado por " + pair.Value.Count + " sitios: "
                    + string.Join(", ", pair.Value) + ". Confirma que nao sao dois donos do mesmo momento.", null);
            }
        }

        private static IEnumerable<string> SerializedEventStrings(MonoBehaviour behaviour)
        {
            var type = behaviour.GetType();
            var serialized = new SerializedObject(behaviour);
            var property = serialized.GetIterator();

            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (property.propertyType != SerializedPropertyType.String) continue;
                if (string.IsNullOrWhiteSpace(property.stringValue)) continue;

                string name = property.propertyPath.Split('.')[0];
                if (GateFieldMarkers.Any(marker => name.ToLowerInvariant().Contains(marker))) continue;
                if (!RaisesField(type, name)) continue;

                yield return property.stringValue;
            }
        }

        /// <summary>
        /// O componente **levanta** este acontecimento, ou apenas **espera** por ele?
        /// Le-se do codigo dele: `Notify(campo)` levanta; `HasSeen(campo)` e
        /// `HasFired(campo)` esperam.
        ///
        /// **Sem esta distincao a ferramenta mente numa coisa que custa caro.** O
        /// `ClimaxStage.climaxEvent` guarda a palavra "climax" e so faz
        /// `HasFired(climaxEvent)` — espera. Contado como dono, fazia parecer que o
        /// `climax` tinha quem o levantasse dentro do apartamento, e que tirar a
        /// estrada das Build Settings era seguro. Nao e: quem o levanta e o
        /// `road_resolved`, e mais ninguem.
        /// </summary>
        private static bool RaisesField(Type type, string fieldName)
        {
            string source = SourceFor(type);
            if (string.IsNullOrEmpty(source)) return false;

            string escaped = Regex.Escape(fieldName);
            return Regex.IsMatch(source, @"(?:Notify|Raise|RaiseEvent|MarkSeen)\s*\(\s*" + escaped + @"\s*[\),]");
        }

        private static void Own(Dictionary<string, List<string>> owners, string id, string by)
        {
            if (!owners.TryGetValue(id, out var list))
            {
                list = new List<string>();
                owners[id] = list;
            }
            if (!list.Contains(by)) list.Add(by);
        }

        private static List<ChapterDefinition> LoadChapters()
        {
            return AssetDatabase.FindAssets("t:ChapterDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ChapterDefinition>)
                .Where(c => c != null)
                .ToList();
        }

        private static HashSet<string> ScanCodeRaisedEvents()
        {
            var found = new HashSet<string>(StringComparer.Ordinal);
            string root = System.IO.Path.Combine(Application.dataPath, "Pungent", "Scripts");
            if (!Directory.Exists(root)) return found;

            var pattern = new Regex("(?:Notify|Raise|RaiseEvent|MarkSeen)\\s*\\(\\s*\"([^\"]+)\"");

            foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                foreach (Match match in pattern.Matches(File.ReadAllText(file)))
                    found.Add(match.Groups[1].Value);

            return found;
        }

        // ----------------------------------------------------------------------------
        // 5. Raisers de Interact sem maneira de serem interagidos. Sem prompt e sem
        //    `afterReading`, e um passo que nunca pode acontecer.
        // ----------------------------------------------------------------------------

        private static void CheckInteractRaisers(List<GameObject> objects, List<Finding> into)
        {
            foreach (var go in objects)
            {
                foreach (var raiser in go.GetComponents<ChapterEventRaiser>())
                {
                    var serialized = new SerializedObject(raiser);

                    var trigger = serialized.FindProperty("raiseOn");
                    if (trigger == null) continue;
                    if (trigger.enumValueIndex < 0 || trigger.enumValueIndex >= trigger.enumNames.Length) continue;
                    if (trigger.enumNames[trigger.enumValueIndex] != "Interact") continue;

                    var prompt = serialized.FindProperty("prompt");
                    var afterReading = serialized.FindProperty("afterReading");

                    bool hasPrompt = prompt != null && !string.IsNullOrWhiteSpace(prompt.stringValue);
                    bool hasReading = afterReading != null && afterReading.objectReferenceValue != null;
                    if (hasPrompt || hasReading) continue;

                    var id = serialized.FindProperty("eventId");
                    Add(into, Severity.Erro, "interact sem entrada",
                        Path(go) + " levanta '" + (id != null ? id.stringValue : "?") + "' por Interact, "
                        + "mas nao tem prompt nem afterReading. Nao ha como o jogador chegar la.", go);
                }
            }
        }

        // ----------------------------------------------------------------------------
        // 6. Dois interagiveis a disputar o mesmo clique. O `PlayerInteractor` escolhe
        //    o primeiro com prompt nao vazio, e "primeiro" e a ordem por que os
        //    componentes foram arrastados para o objecto — que nao e decisao de ninguem.
        // ----------------------------------------------------------------------------

        private static void CheckCompetingInteractables(List<GameObject> objects, List<Finding> into)
        {
            foreach (var go in objects)
            {
                var interactables = go.GetComponents<MonoBehaviour>()
                    .Where(c => c != null && c is IPlayerInteractable)
                    .ToList();
                if (interactables.Count < 2) continue;

                var speaking = new List<string>();
                foreach (var candidate in interactables)
                {
                    string prompt = null;
                    try { prompt = ((IPlayerInteractable)candidate).Prompt; }
                    catch { /* um prompt que dependa de estado de jogo pode rebentar em Edit Mode */ }

                    if (!string.IsNullOrWhiteSpace(prompt))
                        speaking.Add(candidate.GetType().Name + " (\"" + prompt + "\")");
                }

                if (speaking.Count < 2) continue;

                Add(into, Severity.Erro, "dois prompts",
                    Path(go) + " tem " + speaking.Count + " interagiveis com prompt ao mesmo tempo: "
                    + string.Join(", ", speaking) + ". Quem ganha e a ordem dos componentes.", go);
            }
        }

        // ----------------------------------------------------------------------------
        // 7. Ancoras de rotina longe de qualquer movel. A do balcao estava a 25 cm e o
        //    Rui encostava-se ao ar.
        // ----------------------------------------------------------------------------

        private static void CheckRoutineAnchors(List<GameObject> objects, List<Finding> into)
        {
            var furniture = objects
                .SelectMany(o => o.GetComponents<Renderer>())
                .Where(r => r != null && r.enabled && r.bounds.size.magnitude < 8f)
                .ToList();
            if (furniture.Count == 0) return;

            foreach (var go in objects)
            {
                foreach (var anchor in go.GetComponents<RoutineActionAnchor>())
                {
                    var position = anchor.transform.position;

                    float nearest = float.MaxValue;
                    Renderer nearestRenderer = null;

                    foreach (var renderer in furniture)
                    {
                        float distance = Vector3.Distance(renderer.bounds.ClosestPoint(position), position);
                        if (distance >= nearest) continue;
                        nearest = distance;
                        nearestRenderer = renderer;
                    }

                    if (nearestRenderer == null || nearest <= 0.75f) continue;

                    Add(into, Severity.Aviso, "ancora longe",
                        Path(go) + " (" + anchor.AnimatorState + ") esta a " + nearest.ToString("0.00")
                        + " m do movel mais proximo (" + nearestRenderer.gameObject.name + ").", go);
                }
            }
        }

        // ----------------------------------------------------------------------------
        // 8. Raizes das ferramentas. Uma raiz em falta quer dizer que a ferramenta
        //    nunca correu, ou que outra a apagou a seguir — a armadilha de ordem entre
        //    ferramentas, que ja partiu tres coisas num dia.
        // ----------------------------------------------------------------------------

        private static void CheckToolRoots(List<GameObject> objects, List<Finding> into)
        {
            if (SceneManager.GetActiveScene().name != "Apartment_Blockout_V2") return;

            foreach (var root in ApartmentRoots)
            {
                var found = objects.FirstOrDefault(o => o.name == root);
                if (found == null)
                {
                    Add(into, Severity.Erro, "raiz em falta",
                        "'" + root + "' nao existe na cena. A ferramenta que a constroi nao correu, "
                        + "ou outra apagou-a a seguir.", null);
                    continue;
                }

                if (found.transform.childCount != 0) continue;

                Add(into, Severity.Aviso, "raiz vazia",
                    "'" + root + "' existe mas nao tem nada dentro.", found);
            }
        }

        // ----------------------------------------------------------------------------

        private static void Report(List<Finding> findings, List<string> notChecked, int objectCount)
        {
            int errors = findings.Count(f => f.Level == Severity.Erro);
            int warnings = findings.Count(f => f.Level == Severity.Aviso);

            var header = new StringBuilder();
            header.AppendLine("[Verificar] " + errors + " erros, " + warnings + " avisos, em "
                            + objectCount + " objectos de " + SceneManager.sceneCount + " cena(s).");

            if (findings.Count > 0)
            {
                header.AppendLine();
                foreach (var group in findings.GroupBy(f => f.Check).OrderByDescending(g => g.Count()))
                    header.AppendLine("  " + group.Count().ToString().PadLeft(4) + "  " + group.Key);
            }

            header.AppendLine();
            header.AppendLine("O que esta ferramenta NAO verifica:");
            foreach (var item in notChecked) header.AppendLine("  - " + item);

            if (errors > 0) Debug.LogError(header.ToString());
            else if (warnings > 0) Debug.LogWarning(header.ToString());
            else Debug.Log(header + "\nNada a apontar.");

            foreach (var finding in findings.OrderBy(f => f.Level))
            {
                string line = "[Verificar/" + finding.Check + "] " + finding.Message;
                switch (finding.Level)
                {
                    case Severity.Erro: Debug.LogError(line, finding.Context); break;
                    case Severity.Aviso: Debug.LogWarning(line, finding.Context); break;
                    default: Debug.Log(line, finding.Context); break;
                }
            }
        }

        private static void Add(List<Finding> into, Severity level, string check, string message, UnityEngine.Object context)
        {
            into.Add(new Finding { Level = level, Check = check, Message = message, Context = context });
        }

        private static List<GameObject> AllObjects()
        {
            var list = new List<GameObject>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects()) Collect(root.transform, list);
            }
            return list;
        }

        private static void Collect(Transform transform, List<GameObject> into)
        {
            into.Add(transform.gameObject);
            for (int i = 0; i < transform.childCount; i++) Collect(transform.GetChild(i), into);
        }

        private static string Path(GameObject go)
        {
            var stack = new Stack<string>();
            for (var t = go.transform; t != null; t = t.parent) stack.Push(t.name);
            return string.Join("/", stack);
        }
    }
}
