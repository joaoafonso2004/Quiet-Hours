using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Audio;
using Pungent.Narrative;

namespace Pungent.EditorTools
{
    /// <summary>
    /// A passagem de inquietacao da oficina — Dia 4.
    ///
    /// ---
    ///
    /// **O problema deste dia nao e falta de conteudo.** O `GarageDressing` monta a
    /// lona, o monte de pecas, os numeros raspados, a lanterna que veio de casa e a
    /// bagageira; o `GarageCallWiring` monta a meia conversa que fecha o triangulo. O
    /// dia tem tudo o que precisa de ter.
    ///
    /// O que falta e a outra coisa: **em nenhum momento o patio faz nada.** O jogador
    /// anda de um sitio para o outro, apanha peças, ouve uma frase, e vai-se embora.
    /// A oficina e um cenario onde acontecem conversas — e a tese do §5 e que o
    /// espaco tambem tem de ter comportamento.
    ///
    /// ---
    ///
    /// **Quatro momentos, e nenhum precisa de um unico ficheiro novo.**
    ///
    /// Tres deles sao apagar uma luz que ja existe. Isso nao e economia — e a leitura
    /// certa para este dia: o Tomas esta fora de casa, de noite, num sitio que nao
    /// conhece, e o que muda a volta dele e **quanto ele consegue ver**. Nao ha
    /// nenhuma pessoa nova, nenhum vulto, nenhum som que ele nao consiga explicar. O
    /// patio limita-se a ficar mais pequeno de cada vez que ele olha para outro lado.
    ///
    /// | quando | o que |
    /// |---|---|
    /// | encontra os numeros raspados | o patio cala-se durante tres segundos |
    /// | ouve a chamada, ao fundo | a luz do portao apaga-se atras dele |
    /// | carrega as pecas na bagageira | a porta do condutor abre-se |
    /// | acaba de carregar | a luz do escritorio apaga-se, e a camara vai la |
    ///
    /// A ordem importa e esta garantida por acontecimentos e nao por esperanca: o
    /// caminho de volta ao carro so fica escuro **depois** de ele ter ido ao fundo do
    /// patio por vontade propria.
    ///
    /// ---
    ///
    /// Re-executavel. Correr com a `Garage_Blockout` aberta, depois de
    /// 'Rebuild Garage', 'Dress Garage', 'Wire Garage (Day 4)' e 'Wire Garage Call'.
    /// </summary>
    internal static class GarageDreadWiring
    {
        private const string Root = "GARAGE_DREAD";
        private const string AudioFolder = "Assets/ThirdParty/Audio/";

        [MenuItem("Pungent/Blockout/Wire Dread Pass (garage)", false, 17)]
        internal static void Wire()
        {
            if (BuildGuard.Blocked("WireGarageDread")) return;

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Contains("Garage"))
            {
                Debug.LogError("[DreadGaragem] Abrir a `Garage_Blockout` primeiro.");
                return;
            }

            if (GameObject.Find(GarageBlockoutBuilder.RootName) == null)
            {
                Debug.LogError("[DreadGaragem] Sem `GARAGE_BLOCKOUT`. Correr " +
                               "'Rebuild Garage' e 'Dress Garage' primeiro: estes " +
                               "momentos assentam no carro e nas luzes que elas montam.");
                return;
            }

            var old = GameObject.Find(Root);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(Root);
            Undo.RegisterCreatedObjectUndo(root, "Wire garage dread");

            var log = new List<string>();

            SilenceOnEvidence(root.transform, log);
            GateLightDies(root.transform, log);
            DriverDoorOpens(root.transform, log);
            OfficeGoesDark(root.transform, log);

            EditorSceneManager.MarkSceneDirty(scene);

            var report = new System.Text.StringBuilder("[DreadGaragem] Dia 4 ligado.\n");
            foreach (var line in log) report.Append("  ").Append(line).Append('\n');
            Debug.Log(report.ToString());
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// O patio cala-se no instante em que ele percebe o que sao as pecas.
        ///
        /// **Aqui a escuta desce, ao contrario do que se faz no apartamento**, e a
        /// razao e chata mas decisiva: o patio nao tem room tone proprio para calar. O
        /// <see cref="DeadAir"/> apaga o que estiver em ciclo, e se nao estiver nada em
        /// ciclo ele apaga nada. Enquanto a oficina nao tiver o seu proprio fundo — um
        /// zumbido de sodio, vento contra chapa — o silencio tem de vir da escuta.
        ///
        /// Quando esse fundo existir, isto passa a `floor: 1f` e o efeito melhora: os
        /// passos do proprio jogador deixam de ficar longe, e o que desaparece e o
        /// sitio e nao os ouvidos dele.
        /// </summary>
        private static void SilenceOnEvidence(Transform parent, List<string> log)
        {
            float floor = BuildYardTone(parent, log);

            Vector3 at = Anchor("GARAGE_Parts", new Vector3(1.8f, 0f, -4.2f));

            var back = Make(parent, "DEADAIR_Evidence_Return", at + new Vector3(0f, 0.1f, 1.4f));
            var backSource = Source(back, 1f, 12f);

            var go = Make(parent, "DEADAIR_Evidence", at);
            var dead = go.AddComponent<DeadAir>();
            dead.EditorConfigure(DeadAir.Trigger.OnEvent, "evidence_found",
                "arrived_garage", "night_road",
                hold: 3.4f, delay: 1.2f, floor: floor, near: 3f,
                comeback: backSource,
                comebackClip: Existing("KenneyRPG/metalLatch.ogg"));
            EditorUtility.SetDirty(dead);

            log.Add("o patio cala-se 3,4 s ao encontrar os numeros raspados, e volta com um trinco"
                  + (floor >= 1f ? " — cala o patio" : " — cala a escuta (sem fundo para calar)"));
        }

        /// <summary>
        /// O fundo do patio: vento contra chapa, sodio, transito muito ao longe.
        ///
        /// ---
        ///
        /// **Sem isto o silencio da garagem era feito ao contrario.** O
        /// <see cref="DeadAir"/> apaga o que estiver em ciclo, e o patio nao tinha
        /// nada em ciclo para apagar — por isso o silencio tinha de vir de baixar a
        /// **escuta**, que e um truque mais barato e sente-se diferente: em vez de o
        /// sitio desaparecer, sao os ouvidos do jogador que ficam tapados.
        ///
        /// Com um fundo a serio, a `floor` volta a 1 e o efeito passa a ser o que
        /// devia: os proprios passos dele continuam a ouvir-se, e o que desaparece e a
        /// oficina.
        ///
        /// Devolve a `floor` que o `DeadAir` deve usar — 1 quando ha fundo, 0,3 quando
        /// nao ha. A decisao fica aqui e nao escrita a mao la em baixo, para nao se
        /// esquecer de a trocar no dia em que o ficheiro chegar. Que e hoje.
        /// </summary>
        private static float BuildYardTone(Transform parent, List<string> log)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "garage_yard_tone.mp3");
            if (clip == null)
            {
                log.Add("POR PREENCHER: falta `" + AudioFolder + "garage_yard_tone.mp3` — " +
                        "o silencio tem de ser feito baixando a escuta");
                return 0.3f;
            }

            var go = Make(parent, "AMB_GarageYard", new Vector3(-1.2f, 2.5f, -2f));
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = true;

            // Plano de propósito. Um patio nao vem de um ponto: vem de toda a volta, e
            // uma fonte espacial num sitio aberto obrigava o jogador a andar para a
            // ouvir mudar, o que e informacao errada.
            source.spatialBlend = 0f;
            source.volume = 0.22f;
            source.Play();

            log.Add("fundo do patio em ciclo (46,7 s), plano, a 22%");
            return 1f;
        }

        /// <summary>
        /// A luz do portao apaga-se enquanto ele esta ao fundo do patio a ouvir a
        /// chamada.
        ///
        /// **E o unico momento deste dia que muda alguma coisa a serio**, e nao muda
        /// nada que se veja: muda o caminho de volta. Ele foi ate ao fundo por vontade
        /// propria, de costas para o portao, e quando se virar o caminho por onde
        /// entrou esta as escuras.
        ///
        /// Explicacao inocente, e verdadeira: uma lampada de sodio de exterior num
        /// sitio destes apaga-se e volta a acender sozinha o tempo todo.
        /// </summary>
        private static void GateLightDies(Transform parent, List<string> log)
        {
            var lamp = FindLight("Sodium_Gate");
            if (lamp == null)
            {
                log.Add("AVISO: sem `Sodium_Gate` — luz do portao por montar");
                return;
            }

            var spot = GameObject.Find("GARAGE_CallSpot");
            Transform measure = spot != null ? spot.transform : null;
            if (measure == null)
            {
                var holder = Make(parent, "GARAGE_DreadCallSpot", Anchor("GARAGE_BackExit",
                    new Vector3(-7.1f, 0f, 7.2f)));
                measure = holder.transform;
                log.Add("AVISO: sem `GARAGE_CallSpot` — usada a saida das traseiras como referencia");
            }

            var go = Make(parent, "CUE_GateLightDies", lamp.transform.position);
            var source = Source(go, 1f, 20f);

            var cue = go.AddComponent<WitnessedCue>();
            cue.EditorConfigure(WitnessedCue.Effect.LightOff, lamp.transform,
                Vector3.zero, WitnessedCue.Witness.MustNotSee, 7f,
                "heard_call", "night_road",
                measure: measure, light: lamp, theDoor: null,
                source: source, audio: Existing("light_switch_off.mp3"),
                dwell: 1.6f, minDistance: 0f);
            EditorUtility.SetDirty(cue);

            log.Add("a luz do portao apaga-se com ele ao fundo do patio, depois da chamada");
        }

        /// <summary>
        /// O carro assenta nas molas, e ouve-se uma porta.
        ///
        /// ---
        ///
        /// **Isto era outra coisa, e a outra coisa nao era possivel.** A ideia era a
        /// porta do condutor aberta quando ele voltasse da bagageira. O `DOOR_Driver`
        /// do `SEDAN.prefab` **nao e uma porta** — e um transform vazio que serve de
        /// ponto de interaccao, e a carrocaria inteira do carro e uma malha so. Nao ha
        /// folha nenhuma para rodar, e rodar o ponto nao se ve.
        ///
        /// A substituicao e melhor do que o original, e nao e consolacao. Uma porta
        /// aberta e uma pergunta sobre a memoria do jogador — *fechei-a?* — e ele pode
        /// sempre responder que se calhar nao. **Um carro a descair de um lado nao tem
        /// essa saida:** um carro so assenta assim quando lhe entra peso, e ele esta a
        /// tres metros de distancia com as maos numa peca.
        ///
        /// Um grau e tal de inclinacao. Nao ha nada a apontar, nao ha nada a
        /// fotografar, e quem estiver a olhar para a bagageira ouve a porta e volta-se
        /// a tempo de ver um carro perfeitamente normal — ligeiramente mais baixo do
        /// lado do condutor.
        ///
        /// A explicacao inocente continua de pe e e a melhor de todas: ele acabou de
        /// meter quarenta quilos de ferro na bagageira.
        /// </summary>
        private static void DriverDoorOpens(Transform parent, List<string> log)
        {
            var car = FindAnywhere("CAR");
            var boot = FindAnywhere("CAR_BOOT");
            var driverSide = FindAnywhere("DOOR_Driver");
            if (car == null || boot == null)
            {
                log.Add("AVISO: sem `CAR` ou `CAR_BOOT` — o carro a assentar fica por montar. " +
                        "Correr 'Dress Garage' primeiro.");
                return;
            }

            var go = Make(parent, "CUE_CarSettles", car.transform.position);
            var source = Source(go, 0.8f, 14f);

            var cue = go.AddComponent<WitnessedCue>();
            cue.EditorConfigure(WitnessedCue.Effect.Rotate, car.transform,
                // Rolamento para o lado do condutor. Um grau e meio: a mesma ordem de
                // grandeza de uma pessoa a sentar-se, e nao a de um pneu vazio.
                new Vector3(0f, 0f, 1.5f), WitnessedCue.Witness.MustNotSee, 3.6f,
                "seller_met", "night_road",
                measure: boot.transform, light: null, theDoor: null,
                source: source, audio: Existing("LaleksicVarious/door_close.wav"),
                dwell: 2.2f, minDistance: 0f,
                // **O teste de vista nao pode ser contra o carro.**
                //
                // O carro esta a dois metros da cara dele e ocupa meio ecra: perguntar
                // "ele esta a ver o carro?" com o jogador encostado a bagageira da
                // sempre que sim, e a condicao `MustNotSee` nunca podia ser satisfeita.
                // O momento ficava montado, correcto, e nao acontecia uma unica vez —
                // sem erro nenhum a dizer porque.
                //
                // A pergunta certa e sobre o **lado do condutor**, que e o que a
                // carrocaria tapa a quem esta atras. O `DOOR_Driver` nao serve para
                // rodar (nao e uma porta, e um ponto) mas serve exactamente para isto:
                // marca o sitio onde a coisa acontece.
                lookedAt: driverSide != null ? driverSide.transform : null);
            EditorUtility.SetDirty(cue);

            log.Add("o carro assenta 1,5 graus para o lado do condutor e ouve-se uma porta, " +
                    "com ele na bagageira");
        }

        /// <summary>
        /// O escritorio apaga-se, e a camara vai la.
        ///
        /// Duas metades presas por um acontecimento e nao pela ordem em que os
        /// componentes acordam: a luz levanta `garage_office_dark` e o plano espera por
        /// ele. Sem isso, o plano podia agarrar a janela um frame antes de ela ficar
        /// escura — e uma janela iluminada nao diz nada a ninguem.
        ///
        /// O que fica e uma pergunta sem resposta possivel: o homem estava la dentro ha
        /// um minuto, e ninguem passou pelo patio.
        /// </summary>
        private static void OfficeGoesDark(Transform parent, List<string> log)
        {
            var lamp = FindLight("Office_Fluorescent");
            var boot = FindAnywhere("CAR_BOOT");
            if (lamp == null || boot == null)
            {
                log.Add("AVISO: sem `Office_Fluorescent` ou `CAR_BOOT` — escritorio por montar");
                return;
            }

            var go = Make(parent, "CUE_OfficeDark", lamp.transform.position);
            var source = Source(go, 1f, 16f);

            var cue = go.AddComponent<WitnessedCue>();
            cue.EditorConfigure(WitnessedCue.Effect.LightOff, lamp.transform,
                Vector3.zero, WitnessedCue.Witness.MustNotSee, 4.5f,
                "parts_loaded", "night_road",
                measure: boot.transform, light: lamp, theDoor: null,
                source: source, audio: Existing("light_switch_off.mp3"),
                dwell: 1.2f, thoughtText: null, raises: "garage_office_dark",
                minDistance: 0f);
            EditorUtility.SetDirty(cue);

            var snapAt = Make(parent, "SNAP_Office", lamp.transform.position);
            var snap = snapAt.AddComponent<AttentionSnap>();
            snap.EditorConfigure(AttentionSnap.Trigger.OnEvent, "garage_office_dark",
                "parts_loaded", "night_road",
                target: lamp.transform, offset: new Vector3(0f, -0.6f, 0f),
                hold: 1.4f, thoughtText: null, raises: null,
                near: 3f, delay: 0.5f, lineOfSight: false);
            EditorUtility.SetDirty(snap);

            log.Add("a luz do escritorio apaga-se ao acabar de carregar; a camara agarra a janela 1,4 s");
        }

        // ------------------------------------------------------------------

        private static Vector3 Anchor(string name, Vector3 fallback)
        {
            var found = FindAnywhere(name);
            return found != null ? found.transform.position : fallback;
        }

        private static GameObject Make(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            return go;
        }

        private static AudioSource Source(GameObject host, float min, float max)
        {
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = min;
            source.maxDistance = max;
            source.dopplerLevel = 0f;
            return source;
        }

        private static AudioClip Existing(string relativePath)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + relativePath);
            if (clip == null)
                Debug.LogWarning("[DreadGaragem] `" + AudioFolder + relativePath +
                                 "` nao encontrado. O momento fica montado e sem som.");
            return clip;
        }

        private static GameObject FindAnywhere(string name)
        {
            foreach (var t in Object.FindObjectsOfType<Transform>(true))
                if (t.name == name) return t.gameObject;
            return null;
        }

        private static Light FindLight(string name)
        {
            foreach (var l in Object.FindObjectsOfType<Light>(true))
                if (l.gameObject.name == name) return l;
            return null;
        }
    }
}
