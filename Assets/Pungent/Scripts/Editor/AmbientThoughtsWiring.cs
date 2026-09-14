using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Narrative;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Espalha pensamentos de passagem pelo apartamento, **por fase da historia**.
    ///
    /// A maior parte destes nao serve a historia nenhuma e e de proposito. Um jogo
    /// em que o protagonista so comenta o que interessa ensina o jogador, em dois
    /// minutos, que tudo o que ele diz e uma pista — e a partir dai nao ha
    /// surpresas nenhumas. As banalidades sao o que torna as pistas invisiveis.
    ///
    /// Regra ao escrever: **nada aqui pode soar a aviso.** A torneira pinga, o
    /// corredor e frio, o vizinho de baixo tem a televisao alta.
    ///
    /// ---
    ///
    /// **Porque e que isto tem fases.**
    ///
    /// Ate agora havia uma tabela so, aberta em `prologue_done` e nunca fechada.
    /// O mesmo apartamento dizia exactamente as mesmas trinta e uma frases na
    /// primeira manha e na noite em que o Rui anda a procura do jogador. Um
    /// protagonista que atravessa cinco dias e nao muda uma palavra nao esta a
    /// pensar — esta a ler uma placa.
    ///
    /// E pior do que monotonia: **e a unica ferramenta narrativa que esta casa
    /// tem.** O jogo passa-se todo nas mesmas sete divisoes. Se as divisoes nao
    /// mudam de voz, nao ha progressao nenhuma para o jogador sentir — so
    /// objectivos a trocar no canto do ecra.
    ///
    /// **Atencao aos nomes dos acontecimentos.** O `day_two` **nao** abre o Dia 2 —
    /// e levantado pelo `DayTwoDirector.Finish`, que corre quando o Tomas **acorda
    /// na manha do Dia 3**. Quem abre a noite das 02:47 e o `day_one_done`. A
    /// primeira versao destas fases usou `day_two` como fim do Dia 1 e punha a voz
    /// diurna de quem esta a desfazer caixas dentro do corredor as tres da manha.
    /// Os tres marcos, escritos de uma vez para nao haver duvida:
    ///
    /// | `prologue_done` | `day_one_done` | `day_two` |
    /// |---|---|---|
    /// | comeca o Dia 1 | comeca a noite das 02:47 | comeca o Dia 3 |
    ///
    /// Tres fases, e nenhuma e a anterior com medo:
    ///
    /// - <b>Settling</b> (`prologue_done` → `day_one_done`). O Dia 1. Ele tem as
    ///   coisas em sacos e partilha casa com um homem que conheceu no sabado. A voz
    ///   e de quem esta a instalar-se: tudo e novo, provisorio, e ha um
    ///   constrangimento pequeno em usar a casa de outra pessoa.
    ///
    /// - <b>NightOne</b> (`day_one_done` → `day_two`). A noite das 02:47. Poucas
    ///   linhas e curtas — ele esta cansado, a casa esta escura e ele so quer
    ///   voltar para a cama. **O silencio aqui e conteudo**: e a unica parte do jogo
    ///   em que a casa quase nao fala, e e a seguir a esta noite que ele comeca a
    ///   contar coisas.
    ///
    /// - <b>Noticing</b> (`day_two` → `climax`). O Dia 3. Nada nesta fase e uma
    ///   suspeita, e nenhuma frase acusa ninguem de nada. **Ele passou a contar
    ///   coisas.** Quantas canecas, de que lado esta a almofada afundada, que a
    ///   porta dele nunca fecha de todo. Sao observacoes de arrumacao ditas por
    ///   alguem que ja nao consegue nao reparar — que e a forma domestica de dizer
    ///   que a casa deixou de ser confortavel, sem uma unica linha a dizer isso.
    ///
    /// **Nao ha terceira fase.** No `climax` o apartamento cala-se por inteiro (ver
    /// `ClimaxStage`): uma banalidade dita nessa noite nao e uma banalidade, e o
    /// jogo a garantir ao jogador que nao ha perigo nenhum.
    ///
    /// Um objecto pode aparecer nas duas fases com frases diferentes, so numa, ou
    /// so na outra. Os que so existem em `Noticing` sao os que ninguem repara ao
    /// mudar-se e toda a gente repara ao terceiro dia.
    ///
    /// Re-executavel: apaga o que montou antes e volta a montar.
    /// </summary>
    internal static class AmbientThoughtsWiring
    {
        private const string Root = "AMBIENT_THOUGHTS";

        /// <summary>
        /// Em que altura da historia esta frase existe.
        ///
        /// Os nomes dos acontecimentos vivem aqui e num sitio so: uma fase mal
        /// fechada nao da erro nenhum, so uma frase do Dia 1 dita no Dia 3.
        /// </summary>
        private enum Phase
        {
            /// <summary>Dia 1. `prologue_done` → `day_one_done`.</summary>
            Settling,
            /// <summary>A noite das 02:47. `day_one_done` → `day_two`.</summary>
            NightOne,
            /// <summary>Dia 3. `day_two` → `climax`.</summary>
            Noticing
        }

        private static string Opens(Phase phase) => phase switch
        {
            Phase.Settling => "prologue_done",
            Phase.NightOne => "day_one_done",
            _ => "day_two"
        };

        private static string Closes(Phase phase) => phase switch
        {
            Phase.Settling => "day_one_done",
            Phase.NightOne => "day_two",
            _ => "climax"
        };

        /// <summary>objecto na cena, distancia, uma vez so, fase, o que ele pensa.</summary>
        private static readonly (string Target, float Radius, bool Once, Phase When, string Line)[] Table =
        {
            // ================================================================
            // O Rui. O unico que nao e inocente, e ainda assim uma observacao de
            // quem partilha casa e nao de quem desconfia.
            // ================================================================
            ("NPC_Rui", 2.0f, false, Phase.Settling,
                "He smells weird up close. Coffee and something older underneath it."),
            // Nao evoluiu para medo. Evoluiu para a coisa mais banal que ha: ele
            // esta sempre entre o Tomas e a porta. Dito como quem se queixa de um
            // corredor estreito, e o jogador vai verificar a proxima vez.
            ("NPC_Rui", 2.0f, false, Phase.Noticing,
                "Whichever room I am in, he ends up between me and the door. Small flat."),

            // ================================================================
            // O trabalho dele. Quatro linhas que dizem o que ele faz sem uma unica
            // frase de exposicao, e que fazem o Dia 2 significar alguma coisa:
            // quando a internet cai as 02:47, cai a meio de uma entrega.
            // ================================================================
            ("Chair_Desk_Tomas", 1.8f, false, Phase.Settling,
                "Two more scenes to grade and then it uploads overnight."),
            ("Chair_Desk_Tomas", 1.8f, false, Phase.Noticing,
                "I have started leaving it at the exact same angle. So I would know."),

            ("Laptop_Screen", 1.6f, false, Phase.Settling,
                "Ninety gigabytes going up a connection I share with a man I met on Saturday."),
            ("Laptop_Screen", 1.6f, false, Phase.Noticing,
                "It asked me for the password this morning. I never log it out."),

            ("Bookshelf_Tomas", 1.6f, true, Phase.Settling,
                "Colour theory, two semesters of it, and I still eyeball everything."),

            ("NightStand_Tomas", 1.5f, false, Phase.Settling,
                "Alarm at seven. The edit does not care what time I got to bed."),
            ("NightStand_Tomas", 1.5f, false, Phase.Noticing,
                "Charger on the left, phone face down. That is how I leave it."),

            // ================================================================
            // Casa de banho
            // ================================================================
            // Corrigido: ele mudou-se ha dois dias. "Desde o dia em que me mudei"
            // era a voz de um inquilino antigo num homem que ainda tem sacos por
            // abrir — e o jogo inteiro passa-se em cinco dias.
            ("Sink_Bath", 1.6f, false, Phase.Settling,
                "The tap drips. Two nights in and I already know the rhythm of it."),
            ("Mirror_Bath", 1.5f, false, Phase.Settling,
                "I look like I have been awake for two days. I have."),
            ("Toilet", 1.4f, true, Phase.Settling,
                "The seat is loose. Not my flat, not my screwdriver."),
            ("Bathtub", 1.6f, true, Phase.Settling,
                "There is a single long hair on the tiles. Mine is shorter than that."),
            ("Towel_Bath", 1.4f, true, Phase.Settling,
                "Two towels on one rail. I have not worked out which is mine yet."),

            ("Mirror_Bath", 1.5f, false, Phase.Noticing,
                "The cabinet door is open again. It does not stay shut, apparently."),
            ("Towel_Bath", 1.4f, false, Phase.Noticing,
                "Mine is the damp one now. I showered last night, not this morning."),

            // ================================================================
            // Cozinha
            // ================================================================
            ("Kit_Fridge", 1.8f, false, Phase.Settling,
                "Half this shelf is his. He said to help myself and I am not going to."),
            ("Kit_Fridge", 1.8f, false, Phase.Noticing,
                "His half has not moved since Monday. Not one thing."),

            ("Kit_Oven", 1.7f, false, Phase.Settling,
                "Gas rings. I have not used one since my mother's house."),
            ("Kit_Oven", 1.7f, false, Phase.Noticing,
                "He leaves the ring on low sometimes. With nothing on it."),

            ("Kit_Sink", 1.6f, false, Phase.Settling,
                "One mug in the sink. He washed it while I was still saying hello."),
            ("Kit_Sink", 1.6f, false, Phase.Noticing,
                "There is always exactly one mug. Never two, never none."),

            // Corrigido: um homem que se mudou anteontem nao sabe o dia da semana
            // em que o outro poe o lixo la fora. Isso e o que ele sabe ao fim de um
            // mes, e este jogo dura cinco dias.
            ("Kit_Trash", 1.5f, true, Phase.Settling,
                "Bag is new. He must have taken it out before I was up."),
            ("Kit_Trash", 1.5f, true, Phase.Noticing,
                "Third morning, third new bag. I have not once seen him carry one."),

            ("Kit_Table", 1.8f, true, Phase.Settling,
                "Two chairs. I suppose that is the arrangement."),
            ("Kit_Table", 1.8f, true, Phase.Noticing,
                "We have never sat in them at the same time. Not once."),

            ("Kit_Plant", 1.4f, true, Phase.Settling,
                "Somebody waters this. It is not me."),

            // ================================================================
            // Sala
            // ================================================================
            ("Living_Couch", 2.0f, false, Phase.Settling,
                "The cushion on the left is flat. That is his side, then."),
            ("Living_Couch", 2.0f, false, Phase.Noticing,
                "It is warm. I have been in the kitchen about four minutes."),

            ("TV", 2.0f, false, Phase.Settling,
                "Volume always on four. Loud enough to hear, quiet enough to hear past."),
            ("TV", 2.0f, false, Phase.Noticing,
                "On, with the sound right down. He is not even in the room."),

            ("Living_Bookshelf", 1.8f, true, Phase.Settling,
                "Every one of these has a library sticker on the spine."),
            ("Living_Curtains", 1.7f, true, Phase.Settling,
                "Half open. I have not worked out whose half."),
            ("Living_FloorLamp", 1.5f, true, Phase.Settling,
                "This lamp buzzes on the dimmer. Everything here buzzes."),

            ("Living_Curtains", 1.7f, false, Phase.Noticing,
                "I opened these. I know I opened these."),

            // ================================================================
            // Corredor e entrada
            // ================================================================
            ("KeyShelf_Hall", 1.5f, false, Phase.Settling,
                "Keys, wallet, charger. The whole of my life fits here."),
            ("KeyShelf_Hall", 1.5f, false, Phase.Noticing,
                "I have started putting them in my pocket instead. No particular reason."),

            // Corrigido: "levei uma semana a deixar de tentar o 3A" pressupoe uma
            // semana que ele nao teve.
            ("Door_Front_3B", 1.8f, true, Phase.Settling,
                "Third floor, door B. I tried 3A twice on the way up."),
            ("Door_Front_3B", 1.8f, false, Phase.Noticing,
                "The street door downstairs was on the latch again. It is always on the latch."),

            ("ShoeCabinet_Hall", 1.5f, false, Phase.Settling,
                "His boots are facing the door. Mine are not facing anything."),
            ("ShoeCabinet_Hall", 1.5f, false, Phase.Noticing,
                "They are wet. It has not rained since Tuesday."),

            ("ElectricalPanel", 1.5f, true, Phase.Settling,
                "Fuse box in the hall. Good to know, I suppose."),
            ("Trash_Hall", 1.4f, true, Phase.Settling,
                "Smells like the stairwell in here. Everything does."),

            // ================================================================
            // O quarto dele
            // ================================================================
            ("Door_Bedroom_Rui", 1.9f, false, Phase.Settling,
                "His door is never quite shut. Never quite open either."),
            ("Door_Bedroom_Rui", 1.9f, false, Phase.Noticing,
                "Same gap every time. Wide enough to see the hall from in there."),

            // ================================================================
            // O meu quarto
            // ================================================================
            ("Bed_Tomas", 1.7f, false, Phase.Settling,
                "Four hours if I stop now. I am not going to stop now."),
            ("Bed_Tomas", 1.7f, false, Phase.Noticing,
                "I have been sleeping on top of the covers. Easier to get up."),

            ("Window_South_1", 1.9f, false, Phase.Settling,
                "You can see the car from up here. That is why I took this room."),
            ("Window_South_1", 1.9f, false, Phase.Noticing,
                "Still there. I check it more than I used to."),

            ("Carpet_Tomas", 1.6f, true, Phase.Settling,
                "This came with the room. So did the smell of it."),
            ("Dresser_Tomas", 1.5f, true, Phase.Settling,
                "Three drawers, and I have filled one."),

            // ================================================================
            // A NOITE DAS 02:47
            //
            // Nove linhas para a casa inteira, contra as trinta e tal do Dia 1.
            // A escassez e o ponto: e a unica parte do jogo em que ele atravessa
            // o apartamento quase calado, e e por contraste com esse silencio que
            // as linhas do Dia 3 soam a alguem que nao consegue parar de reparar.
            //
            // Curtas, cansadas, e nenhuma sobre o Rui excepto a de o encontrar —
            // que e uma queixa de colega de casa e nao um aviso. Ele so quer
            // voltar para a cama.
            // ================================================================
            ("Door_Bedroom_Rui", 1.9f, false, Phase.NightOne,
                "No light under his door. Good."),
            ("Kit_Oven", 1.7f, false, Phase.NightOne,
                "The ring is on. There is nothing on it."),
            ("Kit_Sink", 1.6f, true, Phase.NightOne,
                "Tap is dripping. I am not fixing it at three in the morning."),
            ("Living_Couch", 2.0f, true, Phase.NightOne,
                "Cushions are flat on both sides tonight."),
            ("TV", 2.0f, true, Phase.NightOne,
                "Standby light. It is the brightest thing in the room."),
            ("ElectricalPanel", 1.5f, true, Phase.NightOne,
                "If it is the fuse I am going back to bed and it can wait."),
            ("Door_Front_3B", 1.8f, true, Phase.NightOne,
                "Chain is off. I am nearly sure I put the chain on."),
            ("Laptop_Screen", 1.6f, false, Phase.NightOne,
                "Forty per cent uploaded. Four hours of it, gone."),
            ("Bed_Tomas", 1.7f, false, Phase.NightOne,
                "Two hours if I go back now. I am not going to sleep either way."),

            // O Rui na cozinha as tres da manha, encostado ao fogao desligado. A
            // fala e sobre **ele proprio**, nao sobre o Rui: uma pessoa apanhada
            // a fazer uma coisa estranha em casa de outra pede desculpa, e e o
            // Tomas que pede.
            ("NPC_Rui", 2.0f, false, Phase.NightOne,
                "He is just standing there. I am the one who feels caught."),
        };

        [MenuItem("Pungent/Blockout/Wire Ambient Thoughts", false, 38)]
        internal static void Wire()
        {
            if (BuildGuard.Blocked("WireAmbientThoughts")) return;

            var scene = EditorSceneManager.GetActiveScene();

            var old = GameObject.Find(Root);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(Root);
            Undo.RegisterCreatedObjectUndo(root, "Wire ambient thoughts");

            // Um grupo por fase. Serve para inspeccionar a cena — abrir o
            // `Noticing` e ver de uma vez tudo o que o Dia 3 acrescentou.
            var groups = new System.Collections.Generic.Dictionary<Phase, Transform>();
            foreach (Phase phase in System.Enum.GetValues(typeof(Phase)))
            {
                var group = new GameObject(phase.ToString());
                group.transform.SetParent(root.transform, false);
                groups[phase] = group.transform;
            }

            int placed = 0, missing = 0;
            var absent = new System.Collections.Generic.HashSet<string>();

            foreach (var entry in Table)
            {
                var target = FindAnywhere(entry.Target);
                if (target == null) { missing++; absent.Add(entry.Target); continue; }

                // O nome leva a fase: o mesmo objecto aparece nas duas tabelas, e
                // dois `THT_Kit_Fridge` irmaos na mesma cena nao se distinguem um
                // do outro ao olhar para a hierarquia.
                var go = new GameObject($"THT_{entry.When}_{entry.Target}");
                go.transform.SetParent(groups[entry.When], false);

                // No sitio da coisa, e nao agarrado a ela: agarra-lo ao objecto
                // fazia o pensamento andar com um movel que alguem arraste, e
                // sujava a hierarquia de props que ja esta montada.
                go.transform.position = Centre(target);

                // Pessoas andam; moveis nao. Quem se mexe leva o pensamento atras,
                // senao ele fica no chao da cozinha a apontar para onde o Rui
                // estava no dia em que a cena foi montada.
                bool moves = target.GetComponentInChildren<Animator>(true) != null
                          || target.GetComponent<UnityEngine.AI.NavMeshAgent>() != null;

                var thought = go.AddComponent<ProximityThought>();
                thought.EditorConfigure(entry.Line, entry.Radius,
                    entry.Once ? ProximityThought.Repeat.Once : ProximityThought.Repeat.Sometimes,
                    thoughtPriority: 1,
                    requires: Opens(entry.When),
                    silencedBy: Closes(entry.When),
                    followTarget: moves ? target.transform : null);
                placed++;
            }

            int silenced = SilenceFlavourAtClimax();

            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[Ambiente] {placed} pensamentos colocados em {groups.Count} fases; " +
                      $"{silenced} objectos do apartamento calados no climax.");
            if (missing > 0)
                Debug.LogWarning($"[Ambiente] {missing} entradas sem objecto na cena: " +
                                 string.Join(", ", absent) + ". A frase existe na tabela e " +
                                 "nao vai sair no jogo.");
        }

        /// <summary>
        /// A outra voz da casa: os `FlavourInteractable` dos moveis.
        ///
        /// O `ClimaxStage` desliga o `AMBIENT_THOUGHTS` inteiro, e isso resolve os
        /// pensamentos de passagem. **Nao resolve estes**, que vivem nos proprios
        /// moveis dentro do `ART_PASS_V2` — e desligar esse grupo era desligar o
        /// apartamento.
        ///
        /// Visto a correr, com o Dia 5 carregado: vinte e dois moveis continuavam a
        /// oferecer "Look" com o Rui a procura do jogador, prontos a responder que
        /// os livros da estante tem etiqueta da biblioteca. E o mesmo defeito dos
        /// pensamentos de passagem, so que noutro componente — e nao se via porque
        /// este exige um clique deliberado, o que o torna mais facil de esquecer e
        /// nao menos errado.
        ///
        /// Aqui e nao no `ApartmentV2Dressing`: aquela ferramenta reconstroi os
        /// moveis todos e nao se corre por causa de um campo. Esta so escreve
        /// `silencedByEvent` e e idempotente. O `requiresEvent` fica como estava.
        ///
        /// Os interagiveis do proprio Dia 5 nao sao tocados: vivem no
        /// `CLIMAX_DAY5`, fora deste grupo, e sao os unicos que devem falar nessa
        /// noite.
        /// </summary>
        private static int SilenceFlavourAtClimax()
        {
            var art = GameObject.Find("ART_PASS_V2");
            if (art == null)
            {
                Debug.LogWarning("[Ambiente] ART_PASS_V2 nao encontrado: os moveis do "
                               + "apartamento ficam a oferecer 'Look' durante o climax.");
                return 0;
            }

            int count = 0;
            foreach (var flavour in art.GetComponentsInChildren<
                         Pungent.Interaction.FlavourInteractable>(true))
            {
                var so = new SerializedObject(flavour);
                var property = so.FindProperty("silencedByEvent");
                if (property == null) continue;
                if (property.stringValue == "climax") { count++; continue; }

                property.stringValue = "climax";
                so.ApplyModifiedPropertiesWithoutUndo();
                count++;
            }

            return count;
        }

        /// <summary>
        /// Pelo centro do que se ve e nao pelo transform: os props deste projecto
        /// sao "holder + malha com offset local", e uma esfera de dois metros no
        /// pivot de um movel fica meio metro ao lado de onde devia.
        /// </summary>
        private static Vector3 Centre(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return target.transform.position;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds.center;
        }

        /// <summary>
        /// `GameObject.Find` so acha objectos activos e de topo por caminho. Os props
        /// estao enterrados em `ART_PASS_V2/&lt;divisao&gt;/`, por isso procura-se por
        /// nome em toda a cena.
        /// </summary>
        private static GameObject FindAnywhere(string name)
        {
            foreach (var t in Object.FindObjectsOfType<Transform>(true))
                if (t.name == name) return t.gameObject;
            return null;
        }
    }
}
