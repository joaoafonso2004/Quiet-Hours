using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Audio;
using Pungent.Interaction;
using Pungent.Narrative;

namespace Pungent.EditorTools
{
    /// <summary>
    /// A passagem de inquietacao do apartamento.
    ///
    /// ---
    ///
    /// **O que faltava.** O `DailyPressureWiring` deu aos dias tempo parado e tres
    /// mudancas encontradas a posteriori — a poltrona rodada, a luz do quarto dele, o
    /// banco da varanda. Sao boas e sao **todas do mesmo tipo**: o jogador esteve
    /// noutra divisao e voltou.
    ///
    /// Um thriller domestico precisa de mais tres tipos, e nenhum deles existia:
    ///
    /// 1. **Som que responde ao jogador.** O andar de cima que anda quando ele anda,
    ///    e que da mais um passo depois de ele parar. Nao ha imagem nenhuma envolvida
    ///    e e o momento mais eficaz da noite.
    /// 2. **Coisas que acontecem com ele na sala.** A luz da rua a fundir-se enquanto
    ///    ele bebe agua. A cadeira atras dele. Nao pode dizer que foi enquanto estava
    ///    noutro sitio, porque nao esteve.
    /// 3. **Silencio.** Quatro segundos em que a casa deixa de existir, e volta com
    ///    uma tabua a estalar.
    ///
    /// ---
    ///
    /// **A escala, dia a dia.** Nao e o mesmo efeito mais alto de cada vez; e um
    /// efeito diferente que reinterpreta o anterior:
    ///
    /// | | 02:47 | Dia 3 | Dia 5 |
    /// |---|---|---|---|
    /// | cima | anda quando tu andas | anda **quando tu paras** | — |
    /// | perto | luz da rua, cadeira | luz da casa de banho, varanda | — |
    /// | silencio | no corredor, ao router | no quarto dele | ao dar pelas chaves |
    /// | plano | a porta dele, de passagem | o quadro electrico | a porta, com ele la fora |
    ///
    /// O Dia 5 leva pouco de proposito: a caca do `RuiHunt` orienta-se por som
    /// filtrado atraves das paredes, e acrescentar passos e ruidos que nao sao dele
    /// nessa noite nao aumenta a tensao — apaga a unica informacao que o jogador tem.
    ///
    /// ---
    ///
    /// **Todos obedecem as regras da <see cref="HouseChange"/>**, que nao sao
    /// negociaveis: pequeno, uma vez so, nunca comentado por ninguem, e sempre com uma
    /// explicacao inocente disponivel. Uma lampada de rua funde-se. O Rui esta na
    /// cozinha e roda uma cadeira. A porta da varanda ficou mal fechada. Nada disto
    /// prova coisa nenhuma — e por isso que fica a doer.
    ///
    /// Re-executavel: destroi e reconstroi so o <see cref="Root"/>.
    /// Correr com a `Apartment_Blockout_V2` aberta, depois do dressing.
    /// </summary>
    internal static class DreadPassWiring
    {
        private const string Root = "DREAD_PASS";
        private const string AudioFolder = "Assets/ThirdParty/Audio/";

        // As janelas dos dias, iguais as do `DailyPressureWiring` — e escritas aqui
        // outra vez de proposito, porque duas ferramentas a partilhar constantes por
        // referencia e uma delas a mudar de janela sem a outra dar por isso.
        //
        // | `day_one_done` | `day_two` | `day3_ready` | `climax` |
        // |---|---|---|---|
        // | comeca a noite das 02:47 | comeca o Dia 3 | ele sai de casa | a noite |
        private const string NightOpens = "day_one_done";
        private const string NightCloses = "day_two";
        private const string Day3Opens = "day_two";
        private const string Day3Closes = "day3_ready";
        private const string ClimaxOpens = "climax";

        [MenuItem("Pungent/Blockout/Wire Dread Pass (apartment)", false, 16)]
        internal static void Wire()
        {
            if (BuildGuard.Blocked("WireDreadPass")) return;

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Contains("Apartment"))
            {
                Debug.LogError("[Dread] Abrir a `Apartment_Blockout_V2` primeiro. " +
                               "Esta ferramenta assenta nos moveis desta cena.");
                return;
            }

            var old = GameObject.Find(Root);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(Root);
            Undo.RegisterCreatedObjectUndo(root, "Wire dread pass");

            var log = new List<string>();
            var missing = new List<string>();

            NightOf0247(root.transform, log, missing);
            DayThree(root.transform, log, missing);
            ClimaxNight(root.transform, log, missing);

            EditorSceneManager.MarkSceneDirty(scene);

            var report = new System.Text.StringBuilder("[Dread] Passagem ligada.\n");
            foreach (var line in log) report.Append("  ").Append(line).Append('\n');
            if (missing.Count > 0)
            {
                report.Append("\n  POR PREENCHER — sem estes ficheiros os momentos ficam mudos:\n");
                foreach (var line in missing) report.Append("    ").Append(line).Append('\n');
            }
            Debug.Log(report.ToString());
        }

        // ==================================================================
        // A NOITE DAS 02:47
        // ==================================================================

        private static void NightOf0247(Transform parent, List<string> log, List<string> missing)
        {
            var group = Group(parent, "NIGHT_0247");

            // ---- 1. Alguem em cima anda quando ele anda ----------------------
            //
            // O primeiro dos tres, e o unico que segue o jogador pela casa toda. Nao
            // tem sitio: acontece onde ele estiver.
            var stepClips = Clips(missing, "upstairs_step1.mp3", "upstairs_step2.mp3",
                                          "upstairs_step3.mp3", "upstairs_step4.mp3");
            AudioClip walkLoop = AsWalkLoop(ref stepClips);

            var upstairs = new GameObject("DREAD_UpstairsSteps_Night");
            upstairs.transform.SetParent(group, false);
            var upSource = upstairs.AddComponent<AudioSource>();
            upSource.playOnAwake = false;
            upSource.spatialBlend = 1f;
            upSource.rolloffMode = AudioRolloffMode.Linear;
            upSource.minDistance = 1.5f;
            upSource.maxDistance = 13f;
            upSource.dopplerLevel = 0f;

            var mirror = upstairs.AddComponent<UpstairsSteps>();
            mirror.EditorConfigure(UpstairsSteps.Mode.Mirrors, stepClips,
                NightOpens, NightCloses, howManyRuns: 2, rest: 50f, height: 3.1f,
                loudness: 0.42f, loop: walkLoop);
            EditorUtility.SetDirty(mirror);
            log.Add("02:47  passos por cima acompanham o jogador (2 vezes, um passo a mais no fim)"
                  + (walkLoop != null ? " — gravacao corrida" : ""));

            // ---- 2. A luz da rua funde-se enquanto ele bebe agua -------------
            //
            // **Com ele a olhar.** Nao ha duvida nenhuma sobre se aconteceu — a duvida
            // passa a ser sobre porque, que e uma duvida pior.
            //
            // A condicao nao pode ser "ele ve o poste": o poste esta dezoito metros
            // abaixo e atras de um vidro, e nao ha raio que atravesse isso de maneira
            // fiavel. A pergunta e outra e o alvo dela e a janela — ver a nota do
            // `sightTarget` no `WitnessedCue`.
            var sink = FindAnywhere("Kit_Sink");
            var streetLamp = FindLight("StreetGlow_N_-8");
            var kitchenWindow = FindAnywhere("Window_North_1");

            if (sink != null && streetLamp != null)
            {
                var go = Make(group, "CUE_StreetlightDies", new Vector3(-5.30f, 1.60f, 4.95f));
                var source = Source(go, 0.9f, 14f);
                var cue = go.AddComponent<WitnessedCue>();
                cue.EditorConfigure(WitnessedCue.Effect.LightOff, streetLamp.transform,
                    Vector3.zero, WitnessedCue.Witness.Ignores, 2.9f,
                    NightOpens, NightCloses,
                    measure: sink.transform, light: streetLamp, theDoor: null,
                    source: source, audio: Clip(missing, "lamp_die.mp3"), dwell: 1.8f,
                    // Zero e obrigatorio aqui: a tarefa do copo de agua prende o
                    // jogador **encostado** ao lava-loica, a menos de um metro do
                    // ponto de medida. Com o minimo por omissao — 1,2 m — a condicao
                    // nunca podia ser satisfeita e o momento nao acontecia nunca, sem
                    // erro nenhum a dizer porque.
                    minDistance: 0f,
                    lookedAt: kitchenWindow != null ? kitchenWindow.transform : null,
                    facingAngle: kitchenWindow != null ? 58f : 0f);
                EditorUtility.SetDirty(cue);
                log.Add("02:47  a luz da rua funde-se com ele ao lava-loica, virado para a janela");
            }
            else log.Add("AVISO: sem `Kit_Sink` ou `StreetGlow_N_-8` — luz da rua por montar");

            // ---- 3. O frigorifico entreaberto atras dele --------------------
            //
            // ---
            //
            // **Isto era uma cadeira a rodar, e a cozinha nao tem cadeiras.** O
            // `Kit_Chair_A` existe como objecto e nao tem malha nenhuma — o momento
            // acontecia, o som tocava, e nao havia nada a mexer-se. E o mesmo tipo de
            // defeito que a poltrona da sala tinha: uma mudanca invisivel nao e uma
            // mudanca fraca, e uma que nunca aconteceu.
            //
            // O frigorifico e melhor do que a cadeira era, e nao e consolacao:
            //
            // - **ve-se.** Uma porta grande aberta contra a parede, com a luz de
            //   dentro a cair no chao. Uma cadeira a rodar 34 graus perde-se no canto
            //   do olho de qualquer maneira;
            // - **explica-se sozinho.** Portas de frigorifico nao pegam sempre. Toda
            //   a gente ja encontrou uma aberta e culpou-se a si propria;
            // - **nao precisa de ficheiro novo.** O `fridge_opening.mp3` esta no
            //   projecto desde o primeiro passe de audio.
            //
            // E ele esta a beber agua ao lava-loica, a 1,4 m dali, de costas.
            var fridgeObject = FindAnywhere("Kit_Fridge");
            var fridge = fridgeObject != null ? fridgeObject.GetComponent<FridgeDoor>() : null;

            if (fridge != null && sink != null)
            {
                var go = Make(group, "CUE_FridgeAjar", fridgeObject.transform.position);
                var source = Source(go, 0.7f, 10f);
                var cue = go.AddComponent<WitnessedCue>();
                cue.EditorConfigure(WitnessedCue.Effect.DoorAjar, fridgeObject.transform,
                    Vector3.zero, WitnessedCue.Witness.MustNotSee, 4.2f,
                    NightOpens, NightCloses,
                    measure: sink.transform, light: null, theDoor: null,
                    source: source, audio: Existing("fridge_opening.mp3"), dwell: 1.5f,
                    minDistance: 0f, theFridge: fridge);
                EditorUtility.SetDirty(cue);
                log.Add("02:47  o frigorifico abre-se atras dele, com a luz de dentro");
            }
            else log.Add("AVISO: sem `Kit_Fridge` com `FridgeDoor` — momento por montar");

            // ---- 4. O silencio, com ele de cocoras no router -----------------
            //
            // Sao os vinte segundos em que o Tomas esta agachado de costas para a
            // casa toda. Tirar-lhe o som durante quatro deles nao acrescenta nada ao
            // ecra e e a coisa mais desagradavel desta noite.
            //
            // A escuta desce tambem: os proprios passos dele ficam longe. E uma das
            // duas vezes no jogo inteiro em que isso acontece.
            var router = FindAnywhere("Router");
            if (router != null)
            {
                var go = Make(group, "DEADAIR_Router", router.transform.position);
                var creak = Make(group, "DEADAIR_Router_Return", new Vector3(2.60f, 0.10f, -0.10f));
                var creakSource = Source(creak, 1.0f, 12f);

                var dead = go.AddComponent<DeadAir>();
                dead.EditorConfigure(DeadAir.Trigger.NearBy, null, NightOpens, NightCloses,
                    hold: 4.2f, delay: 2.6f, floor: 0.22f, near: 1.9f,
                    comeback: creakSource,
                    comebackClip: Existing("LaleksicVarious/floor_creak2.wav"));
                EditorUtility.SetDirty(dead);
                log.Add("02:47  silencio total ao router (4,2 s) e uma tabua no corredor a traze-lo de volta");
            }
            else log.Add("AVISO: sem `Router` — silencio do corredor por montar");

            // ---- 5. O plano da porta dele, de passagem -----------------------
            //
            // Um segundo e dois decimos, sem som e sem pensamento. Nada acontece: a
            // porta esta encostada, como ele proprio disse que estava sempre. O que
            // isto faz e plantar a porta, para a `CHG_RuiRoomLight` do
            // `DailyPressureWiring` ter onde cair no regresso.
            var ruiDoor = FindAnywhere("Door_Bedroom_Rui");
            if (ruiDoor != null)
            {
                var go = Make(group, "SNAP_RuiDoor_Night", new Vector3(-3.60f, 0f, -0.05f));
                var snap = go.AddComponent<AttentionSnap>();
                snap.EditorConfigure(AttentionSnap.Trigger.NearBy, null,
                    NightOpens, NightCloses,
                    target: ruiDoor.transform, offset: new Vector3(0f, 1.35f, 0f),
                    hold: 1.2f, thoughtText: null, raises: null,
                    near: 1.4f, delay: 0f, lineOfSight: true);
                EditorUtility.SetDirty(snap);
                log.Add("02:47  a camara agarra a porta do Rui ao passar no corredor (1,2 s, sem som)");
            }
            else log.Add("AVISO: sem `Door_Bedroom_Rui` — plano do corredor por montar");
        }

        // ==================================================================
        // DIA 3 — a manha em que ele nao esta
        // ==================================================================

        private static void DayThree(Transform parent, List<string> log, List<string> missing)
        {
            var group = Group(parent, "DAY3");

            // ---- 1. Os passos respondem -------------------------------------
            //
            // **Nao e o mesmo efeito outra vez.** Na noite andavam quando ele andava;
            // hoje andam quando ele para. Quem esta em cima deixou de acompanhar e
            // passou a esperar que ele deixe de fazer barulho.
            //
            // E hoje o Rui nao esta em casa, o que tira a explicacao mais confortavel
            // sem ninguem dizer nada.
            var stepClips = Clips(missing, "upstairs_step1.mp3", "upstairs_step2.mp3",
                                          "upstairs_step3.mp3", "upstairs_step4.mp3");
            AudioClip walkLoop = AsWalkLoop(ref stepClips);

            var upstairs = new GameObject("DREAD_UpstairsSteps_Day3");
            upstairs.transform.SetParent(group, false);
            var upSource = upstairs.AddComponent<AudioSource>();
            upSource.playOnAwake = false;
            upSource.spatialBlend = 1f;
            upSource.rolloffMode = AudioRolloffMode.Linear;
            upSource.minDistance = 1.5f;
            upSource.maxDistance = 13f;
            upSource.dopplerLevel = 0f;

            var answers = upstairs.AddComponent<UpstairsSteps>();
            answers.EditorConfigure(UpstairsSteps.Mode.Answers, stepClips,
                Day3Opens, Day3Closes, howManyRuns: 2, rest: 70f, height: 3.1f,
                loudness: 0.38f, loop: walkLoop);
            EditorUtility.SetDirty(answers);
            log.Add("Dia 3  passos por cima so quando ele para (2 vezes)"
                  + (walkLoop != null ? " — gravacao corrida" : ""));

            // ---- 2. O quarto dele nao tem som -------------------------------
            //
            // Ele entra no quarto do Rui por ordem do capitulo, e o quarto e a unica
            // divisao da casa sem room tone nenhum durante tres segundos e meio. A
            // casa volta com a porta atras dele.
            var ruiBed = FindAnywhere("Bed_Rui");
            var ruiDoor = FindAnywhere("Door_Bedroom_Rui");
            if (ruiBed != null)
            {
                var go = Make(group, "DEADAIR_RuiRoom", ruiBed.transform.position);

                Vector3 doorAt = ruiDoor != null
                    ? ruiDoor.transform.position + Vector3.up * 1.1f
                    : new Vector3(-2.45f, 1.10f, -0.80f);
                var back = Make(group, "DEADAIR_RuiRoom_Return", doorAt);
                var backSource = Source(back, 1.0f, 10f);

                var dead = go.AddComponent<DeadAir>();
                dead.EditorConfigure(DeadAir.Trigger.NearBy, null, Day3Opens, Day3Closes,
                    hold: 3.6f, delay: 1.1f, floor: 1f, near: 2.3f,
                    comeback: backSource,
                    comebackClip: Existing("LaleksicVarious/door_creak_open.wav"));
                EditorUtility.SetDirty(dead);
                log.Add("Dia 3  silencio no quarto do Rui (3,6 s), e a porta atras dele a traze-lo de volta");
            }
            else log.Add("AVISO: sem `Bed_Rui` — silencio do quarto por montar");

            // ---- 3. A luz da casa de banho ----------------------------------
            //
            // Acende-se com ele na cozinha, do outro lado da casa. Sem som: e para ser
            // encontrada, e o corredor e caminho obrigatorio para sair de casa.
            //
            // Explicacao inocente: ele proprio a deixou acesa. Nao deixou, mas a
            // primeira coisa que uma pessoa pensa e essa, e e disso que isto vive.
            var bathLight = FindLight("Bathroom_Light");
            var table = FindAnywhere("Kit_Table");
            if (bathLight != null && table != null)
            {
                var go = Make(group, "CUE_BathroomLight", bathLight.transform.position);
                var cue = go.AddComponent<WitnessedCue>();
                cue.EditorConfigure(WitnessedCue.Effect.LightOn, bathLight.transform,
                    Vector3.zero, WitnessedCue.Witness.MustNotSee, 5.2f,
                    Day3Opens, Day3Closes,
                    measure: table.transform, light: bathLight, theDoor: null,
                    source: null, audio: null, dwell: 2.5f, minDistance: 0f);
                EditorUtility.SetDirty(cue);
                log.Add("Dia 3  a luz da casa de banho acende-se com ele na cozinha");
            }
            else log.Add("AVISO: sem `Bathroom_Light` ou `Kit_Table` — luz da casa de banho por montar");

            // ---- 4. A porta da varanda -------------------------------------
            //
            // Com ele preso na lavandaria a por uma maquina — dezasseis segundos de
            // costas, do outro lado do apartamento. Pela porta pelo componente dela e
            // nao pelo transform: ela tem `Rigidbody` e `HingeJoint`.
            var washer = FindAnywhere("WashingMachine");
            var balcony = FindDoor("Door_Balcony");
            if (washer != null && balcony != null)
            {
                var go = Make(group, "CUE_BalconyDoor", balcony.transform.position);
                var cue = go.AddComponent<WitnessedCue>();
                cue.EditorConfigure(WitnessedCue.Effect.DoorAjar, balcony.transform,
                    Vector3.zero, WitnessedCue.Witness.MustNotSee, 3.4f,
                    Day3Opens, Day3Closes,
                    measure: washer.transform, light: null, theDoor: balcony,
                    source: null, audio: null, dwell: 2.0f, minDistance: 0f);
                EditorUtility.SetDirty(cue);
                log.Add("Dia 3  a porta da varanda abre-se com ele na lavandaria (som proprio da porta)");
            }
            else log.Add("AVISO: sem `WashingMachine` ou `Door_Balcony` — varanda por montar");

            // ---- 5. O quadro electrico -------------------------------------
            //
            // Duas metades. A porta do quadro abre-se algures durante a manha, longe
            // dele; e quando ele vai a sair, a camara agarra-a durante um segundo e
            // tres decimos.
            //
            // **Nao ha pensamento nenhum, e e a peca mais importante do dia.** No Dia
            // 5 ele encontra a fechadura do proprio quarto substituida por uma chapa
            // com parafusos novos. Se o jogo lhe disser hoje "alguem andou com uma
            // chave de fendas", a descoberta de sexta-feira ja vem explicada. O que
            // fica hoje e um quadro aberto que ninguem comentou.
            var panel = FindAnywhere("ElectricalPanel");
            if (panel != null)
            {
                // A portinhola, se existir. Rodar o quadro inteiro nao le como uma
                // porta aberta — le como um movel mal pregado a parede, que e um
                // defeito de montagem e nao uma pista.
                Transform leaf = panel.transform.Find("Panel_Door");
                if (leaf == null)
                {
                    leaf = panel.transform;
                    log.Add("AVISO ARTE: `ElectricalPanel` e uma caixa unica de 8x42x32 cm. " +
                            "A portinhola tem de ser um filho chamado `Panel_Door`, com o " +
                            "pivot na dobradica; ate la o que roda e o quadro todo.");
                }

                var opens = Make(group, "CUE_PanelOpen", panel.transform.position);
                var panelSource = Source(opens, 0.5f, 7f);
                var cue = opens.AddComponent<WitnessedCue>();

                // **Menos trinta e oito, e o sinal nao e arbitrario.** A dobradica
                // do modelo esta na aresta de tras do lado norte; medido na cena, a
                // trinta e oito positivos a folha entra 6 mm dentro da parede e a
                // trinta e oito negativos sai 8 cm para o corredor. Uma porta que
                // abre para dentro do gesso nao le como uma porta aberta — le como
                // o modelo estar partido, e ninguem repara que era uma pista.
                //
                // Quem trocar o modelo por outro tem de confirmar isto outra vez:
                // o sentido vem da dobradica, e a dobradica vem do ficheiro.
                cue.EditorConfigure(WitnessedCue.Effect.Rotate, leaf,
                    new Vector3(0f, -38f, 0f), WitnessedCue.Witness.MustNotSee, 9f,
                    Day3Opens, Day3Closes,
                    measure: panel.transform, light: null, theDoor: null,
                    source: panelSource, audio: Existing("KenneyRPG/metalClick.ogg"),
                    dwell: 1.4f, minDistance: 2.6f);
                EditorUtility.SetDirty(cue);

                var snapAt = new GameObject("SNAP_Panel");
                snapAt.transform.SetParent(group, false);
                snapAt.transform.position = new Vector3(5.30f, 0f, -1.35f);
                var snap = snapAt.AddComponent<AttentionSnap>();
                snap.EditorConfigure(AttentionSnap.Trigger.NearBy, null,
                    "day3_chores", Day3Closes,
                    target: panel.transform, offset: new Vector3(0f, 0.15f, 0f),
                    hold: 1.3f, thoughtText: null, raises: null,
                    near: 1.9f, delay: 0f, lineOfSight: true);
                EditorUtility.SetDirty(snap);
                log.Add("Dia 3  o quadro electrico abre-se de manha; a camara agarra-o a saida de casa");
            }
            else log.Add("AVISO: sem `ElectricalPanel` — quadro por montar");
        }

        // ==================================================================
        // DIA 5 — pouco, e de proposito
        // ==================================================================

        private static void ClimaxNight(Transform parent, List<string> log, List<string> missing)
        {
            var group = Group(parent, "CLIMAX");

            // ---- 1. O silencio ao dar pelas chaves --------------------------
            //
            // O `climax_keys_gone` e o pivo da noite: ele pousou as chaves na
            // prateleira ha quatro minutos e nao saiu de casa desde entao. Cinco
            // segundos sem casa nenhuma, com a escuta no chao, e a casa volta com uma
            // tabua vinda de dentro.
            //
            // E a segunda e ultima vez **no apartamento** que a escuta desce. A
            // primeira foi o router, na terca-feira. Uma terceira aqui dentro e o
            // truque a ficar reconhecivel.
            var shelf = FindAnywhere("KeyShelf_Hall");
            Vector3 creakAt = shelf != null
                ? shelf.transform.position + new Vector3(-2.6f, -0.95f, 0.4f)
                : new Vector3(3.00f, 0.10f, -2.40f);

            var back = Make(group, "DEADAIR_KeysGone_Return", creakAt);
            var backSource = Source(back, 1.0f, 12f);

            var go = Make(group, "DEADAIR_KeysGone", creakAt);
            var dead = go.AddComponent<DeadAir>();
            dead.EditorConfigure(DeadAir.Trigger.OnEvent, "climax_keys_gone",
                ClimaxOpens, null,
                hold: 5f, delay: 1.4f, floor: 0.18f, near: 3f,
                comeback: backSource,
                comebackClip: Existing("LaleksicVarious/floor_creak1.wav"));
            EditorUtility.SetDirty(dead);
            log.Add("Dia 5  silencio total ao dar pela prateleira vazia (5 s) e uma tabua dentro de casa");

            // ---- 2. A porta, com ele la fora --------------------------------
            //
            // Quando as chaves aparecem em cima das coisas dele, a camara vira-se para
            // a porta do quarto — que e a porta do quarto onde o jogador esta neste
            // momento. O `keys_found` levanta `rui_awake` logo a seguir: um segundo e
            // meio a olhar para uma porta fechada, e a noite comeca.
            var ruiDoor = FindAnywhere("Door_Bedroom_Rui");
            if (ruiDoor != null)
            {
                var snapAt = Make(group, "SNAP_KeysFound", ruiDoor.transform.position);
                var snap = snapAt.AddComponent<AttentionSnap>();
                snap.EditorConfigure(AttentionSnap.Trigger.OnEvent, "keys_found",
                    ClimaxOpens, null,
                    target: ruiDoor.transform, offset: new Vector3(0f, 1.35f, 0f),
                    hold: 1.6f, thoughtText: null, raises: null,
                    near: 3f, delay: 0.7f, lineOfSight: true);
                EditorUtility.SetDirty(snap);
                log.Add("Dia 5  a camara agarra a porta do quarto ao encontrar as chaves (1,6 s)");
            }
            else log.Add("AVISO: sem `Door_Bedroom_Rui` — plano das chaves por montar");
        }

        // ==================================================================
        // Utensilios
        // ==================================================================

        private static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject Make(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            return go;
        }

        /// <summary>Fonte 3D com as mesmas regras do resto do jogo: linear, sem doppler.</summary>
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

        /// <summary>
        /// Um clip novo, dos que ainda nao existem no projecto.
        ///
        /// **Nao existir nao e erro.** O momento fica montado e calado, e o nome do
        /// ficheiro que falta vai para o relatorio — que e o unico sitio onde alguem o
        /// vai ler. Uma ferramenta que rebenta por falta de um som deixava o resto da
        /// passagem por montar.
        /// </summary>
        private static AudioClip Clip(List<string> missing, string fileName)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + fileName);
            if (clip == null && !missing.Contains(AudioFolder + fileName))
                missing.Add(AudioFolder + fileName);
            return clip;
        }

        /// <summary>
        /// Decide se o que chegou sao passos soltos ou uma gravacao corrida.
        ///
        /// ---
        ///
        /// **Pelo comprimento, e nao por um campo que alguem tem de se lembrar de
        /// marcar.** Um passo dura menos de um segundo; uma gravacao de alguem a andar
        /// dura dezenas. Nao ha ambiguidade nenhuma entre os dois, e ler o ficheiro e
        /// mais fiavel do que confiar em quem o largou na pasta.
        ///
        /// Existe porque foi o que aconteceu: pediram-se quatro passos soltos e chegou
        /// **um ficheiro de trinta e quatro segundos**. Disparado como `PlayOneShot` a
        /// cada 0,66 s, isso empilhava cinquenta copias de si proprio em vez de soar a
        /// alguem a andar. O formato que chegou nao esta errado — e mais facil de
        /// gravar e soa melhor, porque a cadencia e de uma pessoa a serio. O que
        /// estava errado era so a maneira de o tocar.
        ///
        /// Devolve o ciclo e tira-o da lista de passos. Quando os quatro curtos
        /// chegarem, isto nao encontra nada com mais de dois segundos e meio e a
        /// ferramenta volta sozinha aos disparos.
        /// </summary>
        private static AudioClip AsWalkLoop(ref AudioClip[] clips)
        {
            const float LongEnoughToBeAWalk = 2.5f;

            if (clips == null || clips.Length == 0) return null;

            AudioClip loop = null;
            var shorts = new List<AudioClip>();

            foreach (var clip in clips)
            {
                if (clip == null) continue;
                if (clip.length >= LongEnoughToBeAWalk && loop == null) loop = clip;
                else shorts.Add(clip);
            }

            clips = shorts.ToArray();
            return loop;
        }

        private static AudioClip[] Clips(List<string> missing, params string[] fileNames)
        {
            var found = new List<AudioClip>();
            foreach (string name in fileNames)
            {
                var clip = Clip(missing, name);
                if (clip != null) found.Add(clip);
            }
            return found.ToArray();
        }

        /// <summary>Um clip que ja existe no projecto. Avisa alto se tiver mudado de sitio.</summary>
        private static AudioClip Existing(string relativePath)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + relativePath);
            if (clip == null)
                Debug.LogWarning("[Dread] `" + AudioFolder + relativePath + "` desapareceu. " +
                                 "O momento fica montado e sem o som que o fecha.");
            return clip;
        }

        /// <summary>
        /// Avisa quando o movel que vai mudar de sitio nao tem malha nenhuma.
        ///
        /// Vale a pena estar aqui, porque e uma falha que **nao da erro e nao se ve a
        /// jogar**: o componente monta, a condicao cumpre-se, a rotacao acontece, o som
        /// toca, e o jogador nao ve nada mexer porque nao ha nada. Uma mudanca invisivel
        /// nao e uma mudanca fraca — e uma mudanca que nunca aconteceu, e gasta a
        /// unica oportunidade que aquele momento tinha.
        ///
        /// Varios moveis do apartamento sao hoje transforms vazios a espera de modelo.
        /// Um deles, o `Living_Armchair`, ja e alvo da `CHG_Armchair` do
        /// `DailyPressureWiring` desde que essa ferramenta existe.
        /// </summary>
        private static void WarnIfInvisible(GameObject prop, string moment, List<string> log)
        {
            if (prop == null || prop.GetComponentInChildren<Renderer>(true) != null) return;
            log.Add("AVISO ARTE: `" + prop.name + "` nao tem malha nenhuma — " + moment +
                    " acontece e nao se ve. Por agora so sai o som.");
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

        private static DoorDragInteractable FindDoor(string name)
        {
            foreach (var d in Object.FindObjectsOfType<DoorDragInteractable>(true))
                if (d.gameObject.name == name) return d;
            return null;
        }
    }
}
