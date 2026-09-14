using Pungent.Interaction;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// Monta o Dia 3: a casa vazia, e a porta dele aberta.
    ///
    /// ---
    ///
    /// **O dia inteiro assenta numa ausencia.** Os outros dias comecam com o Rui a
    /// viver a casa — na cozinha, no sofa, a passar no corredor. Este comeca sem ele,
    /// e e a **primeira vez** que isso acontece. O jogador nao precisa que ninguem
    /// lho diga: passa por duas divisoes e a casa esta calada de uma maneira que
    /// ainda nao tinha estado.
    ///
    /// **A porta do quarto dele fica aberta, e e a coisa mais violenta do dia.**
    /// Esteve sempre encostada — o prologo inteiro e construido a volta de uma porta
    /// entreaberta que nao e a do jogador. Hoje esta escancarada e nao ha ninguem
    /// dentro. O quarto dele deixa de ser um sitio proibido e passa a ser uma coisa
    /// que se pode ver, e o jogador vai olhar, e ao olhar percebe que ele **queria**
    /// que se olhasse.
    ///
    /// Nao ha objectivo nenhum a apontar para la. Um objectivo transformava isto numa
    /// tarefa; assim e uma decisao que o jogador toma sozinho e da qual nao pode
    /// culpar o jogo.
    ///
    /// ---
    ///
    /// **Porque e um `Stage` e nao logica espalhada.** O `PrologueStage` e o
    /// `ClimaxStage` ja provaram a regra neste projecto: uma noite que precisa de
    /// meia duzia de coisas ligadas ao mesmo tempo precisa de **um dono**. Espalhar
    /// isto por cinco componentes com o mesmo `requiresEvent` da cinco maneiras de
    /// meio dia acontecer.
    ///
    /// Repoe tudo ao sair, para o Dia 4 nao herdar uma casa sem Rui.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DayThreeStage : MonoBehaviour
    {
        [Tooltip("Acontecimento que monta o dia.")]
        [SerializeField] private string stageOnEvent = "day_two";

        [Tooltip("Acontecimento que o desmonta e devolve o Rui a casa.")]
        [SerializeField] private string endOnEvent = "day3_ready";

        [Header("O Rui fora de casa")]
        [SerializeField] private GameObject rui;

        [Tooltip("Onde ele fica enquanto nao esta em casa. Fora da NavMesh e fora de "
               + "vista.\n\nNao e desactivado: um NPC desligado perde o agente, a "
               + "rotina e o sitio, e voltar a liga-lo a meio de um corredor e como "
               + "se apanham NPCs a atravessar paredes.")]
        [SerializeField] private Vector3 awaySpot = new Vector3(9.5f, -4f, 9.5f);

        [Tooltip("Componentes dele que ficam calados enquanto nao esta. A rotina "
               + "domestica e a presenca sonora sao as duas que denunciariam que ele "
               + "ainda esta algures na casa.")]
        [SerializeField] private MonoBehaviour[] silenced = new MonoBehaviour[0];

        [Header("A porta dele")]
        [Tooltip("A porta do quarto do Rui. Hoje fica **aberta**, e e a leitura toda "
               + "do dia. Ver a nota da classe.")]
        [SerializeField] private DoorDragInteractable ruiDoor;

        [Tooltip("Territorio do quarto dele. Calado hoje: nao ha ninguem em casa para "
               + "expulsar ninguem, e ser expulso por uma casa vazia seria o jogo a "
               + "desmentir-se.")]
        [SerializeField] private MonoBehaviour roomTerritory;

        [Header("A casa vazia")]
        [Tooltip("Luzes que ficam apagadas. Uma casa com o dono fora nao tem luzes "
               + "acesas as oito da manha.")]
        [SerializeField] private Light[] lightsOff = new Light[0];

        [SerializeField] private ChapterDirector director;

        private bool staged;
        private bool ended;
        private Vector3 ruiWasAt;
        private Quaternion ruiWasFacing;
        private bool[] silencedWas;
        private bool territoryWas;

        /// <summary>Verdadeiro enquanto o Rui esta fora de casa.</summary>
        public bool HouseIsEmpty => staged && !ended;

        private void Update()
        {
            director = ChapterDirector.Resolve(director);
            if (director == null) return;

            if (!staged && !string.IsNullOrWhiteSpace(stageOnEvent)
                && director.HasSeen(stageOnEvent))
                Stage();

            if (staged && !ended && !string.IsNullOrWhiteSpace(endOnEvent)
                && director.HasSeen(endOnEvent))
                Restore();
        }

        private void Stage()
        {
            staged = true;

            if (rui != null)
            {
                ruiWasAt = rui.transform.position;
                ruiWasFacing = rui.transform.rotation;

                var agent = rui.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.enabled) agent.enabled = false;

                rui.transform.position = awaySpot;
            }

            silencedWas = new bool[silenced.Length];
            for (int i = 0; i < silenced.Length; i++)
            {
                if (silenced[i] == null) continue;
                silencedWas[i] = silenced[i].enabled;
                silenced[i].enabled = false;
            }

            if (roomTerritory != null)
            {
                territoryWas = roomTerritory.enabled;
                roomTerritory.enabled = false;
            }

            // Aberta, e nao encostada. Uma porta entreaberta le-se como descuido; uma
            // porta aberta de par em par com a casa vazia le-se como convite.
            if (ruiDoor != null) ruiDoor.SetOpen(true);

            for (int i = 0; i < lightsOff.Length; i++)
                if (lightsOff[i] != null) lightsOff[i].enabled = false;
        }

        /// <summary>
        /// Devolve o Rui a casa. Chamado quando o jogador se prepara para sair — e a
        /// partir daqui ele volta a estar la, o que o <see cref="NPC.DoorConversation"/>
        /// precisa para bater a porta.
        /// </summary>
        private void Restore()
        {
            ended = true;

            if (rui != null)
            {
                rui.transform.position = ruiWasAt;
                rui.transform.rotation = ruiWasFacing;

                var agent = rui.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && !agent.enabled) agent.enabled = true;
            }

            if (silencedWas != null)
                for (int i = 0; i < silenced.Length && i < silencedWas.Length; i++)
                    if (silenced[i] != null) silenced[i].enabled = silencedWas[i];

            if (roomTerritory != null) roomTerritory.enabled = territoryWas;

            // A porta dele volta a estar fechada, e ninguem viu fechar. E a mesma
            // regra da casa toda neste jogo: as coisas mudam quando nao se esta a ver.
            if (ruiDoor != null) ruiDoor.SetOpen(false);
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(GameObject ruiObject, DoorDragInteractable door,
            MonoBehaviour territory, MonoBehaviour[] toSilence, Light[] toDim)
        {
            rui = ruiObject;
            ruiDoor = door;
            roomTerritory = territory;
            silenced = toSilence;
            lightsOff = toDim;
        }
#endif
    }
}
