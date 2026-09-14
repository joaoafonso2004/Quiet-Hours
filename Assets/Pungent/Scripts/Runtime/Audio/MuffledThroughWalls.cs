using UnityEngine;

namespace Pungent.Audio
{
    /// <summary>
    /// Uma fonte de som que se ouve abafada quando ha parede pelo meio.
    ///
    /// A seccao 5 pede, para o Dia 5, que "sons filtrados por paredes e portas
    /// orientem a navegacao". E a mecanica de orientacao inteira desse capitulo: a
    /// casa esta as escuras, o Rui nao aparece no mapa e nao ha marcador nenhum —
    /// o que diz ao jogador onde ele esta e o som dos passos e das portas, e o que
    /// lhe diz **quao perto** e o quanto esse som esta abafado.
    ///
    /// Sem isto o som atravessa as paredes com a mesma nitidez e o jogador nao
    /// consegue distinguir "ele esta do outro lado desta porta" de "ele esta na
    /// cozinha". Perde-se a unica informacao de que precisa para decidir por onde ir.
    ///
    /// Feito com uma linha de vista e um passa-baixo, e nao com oclusao a serio: a
    /// estrada da noite ja usou o mesmo truque para a radio do carro se ouvir de
    /// fora, e um sistema de propagacao de audio nao faz existir nada que nao possa
    /// ja existir hoje.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class MuffledThroughWalls : MonoBehaviour
    {
        [Tooltip("Corte com linha de vista limpa. Alto = som inteiro.")]
        [SerializeField, Min(500f)] private float openCutoff = 22000f;

        [Tooltip("Corte com parede pelo meio. A 780 Hz ouve-se que ha ali alguem "
               + "sem se perceber o que e — o mesmo valor da radio abafada da estrada.")]
        [SerializeField, Min(100f)] private float blockedCutoff = 780f;

        [Tooltip("Volume relativo com parede pelo meio.")]
        [SerializeField, Range(0.05f, 1f)] private float blockedVolume = 0.55f;

        [Tooltip("Velocidade da transicao. Instantanea era um clique audivel cada "
               + "vez que ele passasse por um vao.")]
        [SerializeField, Min(0.5f)] private float blendSpeed = 5f;

        [Tooltip("Quantas vezes por segundo se testa a linha de vista. Nao precisa "
               + "de ser todos os frames: o que muda e uma parede, nao um pixel.")]
        [SerializeField, Min(1f)] private float checksPerSecond = 10f;

        [SerializeField] private Transform listener;

        private AudioSource source;
        private AudioLowPassFilter lowPass;
        private float baseVolume;
        private float blocked;
        private float nextCheckAt;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            baseVolume = source.volume;

            lowPass = GetComponent<AudioLowPassFilter>();
            if (lowPass == null) lowPass = gameObject.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = openCutoff;

            ResolveListener();
        }

        /// <summary>
        /// O ouvinte e a camara do jogador, e a camara do jogador e outra em cada
        /// cena. Procurada aqui e nao guardada na cena para este componente poder
        /// viver num prefab sem ficar com uma referencia morta.
        /// </summary>
        private void ResolveListener()
        {
            if (listener != null) return;

            var audioListener = FindObjectOfType<AudioListener>();
            if (audioListener != null) { listener = audioListener.transform; return; }

            if (Camera.main != null) listener = Camera.main.transform;
        }

        private void Update()
        {
            if (listener == null) { ResolveListener(); return; }

            if (Time.time >= nextCheckAt)
            {
                nextCheckAt = Time.time + 1f / checksPerSecond;
                blocked = IsBlocked() ? 1f : 0f;
            }

            float t = 1f - Mathf.Exp(-blendSpeed * Time.deltaTime);
            lowPass.cutoffFrequency = Mathf.Lerp(lowPass.cutoffFrequency,
                Mathf.Lerp(openCutoff, blockedCutoff, blocked), t);
            source.volume = Mathf.Lerp(source.volume,
                baseVolume * Mathf.Lerp(1f, blockedVolume, blocked), t);
        }

        /// <summary>
        /// Ha alguma coisa solida entre esta fonte e o ouvinte?
        ///
        /// Ignora triggers de proposito: a casa esta cheia de volumes de
        /// acontecimento e de esconderijos, e um deles a passar a frente abafava o
        /// som sem haver parede nenhuma.
        /// </summary>
        private bool IsBlocked()
        {
            Vector3 from = transform.position;
            Vector3 to = listener.position;
            Vector3 direction = to - from;
            float distance = direction.magnitude;
            if (distance < 0.2f) return false;

            return Physics.Raycast(from, direction / distance, out RaycastHit hit,
                       distance, ~0, QueryTriggerInteraction.Ignore)
                   // O proprio corpo de quem faz o som nao conta como parede.
                   && !hit.transform.IsChildOf(transform.root);
        }
    }
}
