using UnityEngine;
using UnityEngine.AI;

namespace Pungent.NPC
{
    /// <summary>
    /// Tira o Rui de onde ele nao devia ter conseguido chegar.
    ///
    /// ---
    ///
    /// **O que aconteceu.** Com o objectivo *"lock your bedroom door"* no ecra, o Rui
    /// atravessou a porta trancada do quarto do Tomas e ficou la dentro, parado, para
    /// o resto da noite.
    ///
    /// Nao foi a porta que falhou. Os sete `NavMeshObstacle` das portas estao todos
    /// activos e a escavar — medido — e o do quarto do Tomas cobre exactamente a
    /// folha: 0,90 x 2,10, no mesmo centro. O problema e o **momento**: um obstaculo
    /// que escava debaixo de um agente que esta no vao nao o empurra para tras, tira-
    /// lhe o chao. Ele fica do lado onde estiver, e do lado de dentro nao ha NavMesh
    /// nenhuma para ele voltar — `isOnNavMesh` passa a falso e o agente deixa de
    /// aceitar destinos. Parado, dentro do quarto, sem erro nenhum na consola.
    ///
    /// **Nao se corrige nas portas.** Desligar o carve devolvia o problema original —
    /// o Rui a atravessar portas fechadas a vontade. Sincronizar o carve com o
    /// momento em que ninguem esta no vao e uma coordenacao entre sete portas e um
    /// agente que se parte na primeira excepcao.
    ///
    /// Corrige-se onde o sintoma vive: **um agente sem chao debaixo dele volta ao
    /// chao.** Uma vez por segundo, e so quando esta mesmo perdido.
    ///
    /// ---
    ///
    /// **E de propósito que isto nao esconde nada.** Se ele for encontrado fora da
    /// NavMesh, e escrito na consola com o sitio — o teleporte e a rede, nao a
    /// solucao. Um jogo em que o Rui aparece a saltar de sitio uma vez por minuto tem
    /// outro problema, e esta linha e a unica maneira de dar por ele.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NpcNavRecovery : MonoBehaviour
    {
        [Tooltip("De quanto em quanto tempo se verifica. Nao precisa de ser depressa: "
               + "isto e uma rede e nao um sistema de movimento.")]
        [SerializeField, Min(0.2f)] private float checkEvery = 1f;

        [Tooltip("A que distancia se procura chao valido a volta dele.\n\n"
               + "Generoso: um quarto tem tres metros de lado, e o objectivo e "
               + "devolve-lo ao corredor e nao ao canto onde ficou preso.")]
        [SerializeField, Min(0.5f)] private float searchRadius = 4f;

        [Tooltip("Escreve na consola sempre que recuperar. Deixar ligado: uma "
               + "recuperacao e um sintoma, e as silenciosas nunca sao investigadas.")]
        [SerializeField] private bool logRecoveries = true;

        private NavMeshAgent agent;
        private float nextCheck;

        /// <summary>Quantas vezes teve de o salvar. Util para inspeccionar e testes.</summary>
        public int Recoveries { get; private set; }

        private void Awake() => agent = GetComponent<NavMeshAgent>();

        private void Update()
        {
            if (agent == null || !agent.enabled) return;
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + checkEvery;

            if (agent.isOnNavMesh) return;

            // `SamplePosition` procura a partir da posicao actual: se ele estiver
            // dentro de um quarto escavado, o ponto valido mais proximo e a soleira do
            // outro lado da porta, que e exactamente para onde ele deve voltar.
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit,
                    searchRadius, NavMesh.AllAreas))
            {
                if (logRecoveries)
                    Debug.LogWarning("[NavRede] " + name + " esta fora da NavMesh em " +
                                     transform.position.ToString("F2") + " e nao ha chao " +
                                     "valido a " + searchRadius.ToString("F1") + " m. " +
                                     "Ou o raio e curto de mais, ou falta NavMesh aqui.");
                return;
            }

            Vector3 from = transform.position;

            // `Warp` e nao `transform.position`: e o unico que repoe o estado interno
            // do agente. Escrever no transform devolve-o ao chao visualmente e deixa-o
            // a pensar que continua perdido.
            agent.Warp(hit.position);
            Recoveries++;

            if (logRecoveries)
                Debug.LogWarning("[NavRede] " + name + " estava fora da NavMesh em " +
                                 from.ToString("F2") + "; devolvido a " +
                                 hit.position.ToString("F2") + " (" + Recoveries + "a vez). " +
                                 "Costuma ser um obstaculo de porta a escavar com ele no vao.");
        }

#if UNITY_EDITOR
        /// <summary>Usado pelas ferramentas de ligacao, que vivem noutra assembly.</summary>
        public void EditorConfigure(float interval, float radius)
        {
            checkEvery = interval;
            searchRadius = radius;
        }
#endif
    }
}
