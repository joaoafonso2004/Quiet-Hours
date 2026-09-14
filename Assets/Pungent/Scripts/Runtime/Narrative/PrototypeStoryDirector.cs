using Pungent.Interaction;
using UnityEngine;

namespace Pungent.Narrative
{
    [DisallowMultipleComponent]
    public sealed class PrototypeStoryDirector : MonoBehaviour
    {
        [SerializeField] private PrototypeHUD hud;
        [Tooltip("Legado: mantido para as cenas antigas. Se estiver preenchido "
               + "entra tambem na lista de luzes que se apagam.")]
        [SerializeField] private Light corridorLight;
        [Tooltip("Luzes que se apagam quando o router volta. Tem de incluir a luz "
               + "que esta por cima do router, senao o jogador le a fala sobre uma "
               + "luz que se apagou fora do seu campo de visao e nao percebe nada.")]
        [SerializeField] private Light[] lightsThatDie;
        [SerializeField] private Light routerRedLight;
        
        [SerializeField] private GameObject routerRedIndicator;
        [SerializeField] private GameObject routerGreenIndicator;
[SerializeField] private Light routerGreenLight;
        [SerializeField] private OpeningQuestDirector quest;
        [Tooltip("Legado. O anuncio da queda da ligacao passou para o PlayerDeskOpening, "
               + "que o mostra no momento em que ela cai em vez de no arranque da cena, "
               + "por tras do texto de abertura.")]
        [SerializeField] private bool announceOnStart;
        private bool routerRestarted;

        private void Start()
        {
            if (quest == null) quest = FindObjectOfType<OpeningQuestDirector>();

            // Com a cadeia de objetivos ativa, e ela que escreve o objetivo.
            if (quest == null)
                hud?.SetObjective("OBJECTIVE: The internet is down. Restart the router at the end of the hall.");

            if (announceOnStart) hud?.ShowMessage("02:47 \u2014 Connection lost", 4f);
            // A primeira noite ainda nao teve falha nenhuma. Se a quest das 02:47
            // ja estiver activa, e ela que manda e Start nao lhe volta a ligar o
            // router por causa da ordem de execucao entre componentes.
            if (quest == null || !quest.isActiveAndEnabled)
                SetRouterOnline(true);
        }

        public void OnRouterRestarted()
        {
            if (routerRestarted) return;
            routerRestarted = true;
            SetRouterState(true);

            if (corridorLight != null) corridorLight.enabled = false;
            if (lightsThatDie != null)
                foreach (var light in lightsThatDie)
                    if (light != null) light.enabled = false;

            hud?.ShowMessage("The connection is back. The hallway went dark with it.", 5f);

            // O objetivo passa a ser gerido pela cadeia da abertura: aqui so se
            // reporta o facto, para o router nao ter de saber da historia.
            if (quest != null) quest.NotifyRouterRestarted();
            else hud?.SetObjective("OBJECTIVE: Return to your room.");
        }

        public void SetRouterOnline(bool online)
        {
            routerRestarted = online;
            SetRouterState(online);
        }

        private void SetRouterState(bool online)
        {
            if (routerRedLight != null) routerRedLight.enabled = !online;
            if (routerGreenLight != null) routerGreenLight.enabled = online;
            if (routerRedIndicator != null) routerRedIndicator.SetActive(!online);
            if (routerGreenIndicator != null) routerGreenIndicator.SetActive(online);
        }
    }
}
