using Pungent.Narrative;
using UnityEngine;

namespace Pungent.Driving
{
    /// <summary>
    /// O que o Tomas vai pensando ao longo da estrada.
    ///
    /// Tres minutos a conduzir de noite numa estrada vazia, sem uma unica linha,
    /// e onde um jogador pergunta "para onde e que eu vou, ao certo?". A regra e
    /// simples: em cada minuto e picos ha uma frase que relembra o objectivo sem
    /// o anunciar.
    ///
    /// Por metros e nao por tempo. Quem parar na berma a olhar para as arvores nao
    /// leva com o mesmo pensamento de dez em dez segundos, e quem for depressa
    /// ouve-os todos na mesma — a estrada e que os entrega, nao o relogio.
    ///
    /// Nenhum e assustador. O medo desta cena vem do par de farois no espelho e do
    /// silencio, e um narrador a dizer que esta assustado tira o trabalho ao
    /// jogador. O que estes fazem e outra coisa: dizem para onde ele vai, o que
    /// leva na mala e a que horas conta chegar. Sao ancoras, nao atmosfera.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoadThoughts : MonoBehaviour
    {
        [System.Serializable]
        public struct Beat
        {
            [Tooltip("A quantos metros de estrada este pensamento entra.")]
            public float AtMetres;

            [TextArea] public string Line;

            [Tooltip("So sai depois de o par de farois ja ter aparecido.")]
            public bool NeedsFollower;
        }

        [SerializeField] private RoadPath path;
        [SerializeField] private CarDriver car;
        [SerializeField] private PlayerThoughtDirector thoughts;
        [SerializeField] private NightRoadDirector director;

        [SerializeField] private Beat[] beats =
        {
            new Beat { AtMetres = 260f,
                Line = "Forty minutes of this and then my own bed." },
            new Beat { AtMetres = 900f,
                Line = "Ninety euros and a boot full of someone else's parts." },
            new Beat { AtMetres = 1500f,
                Line = "Fit them Saturday. Then the car is done and I can stop thinking about it." },
            new Beat { AtMetres = 2100f, NeedsFollower = true,
                Line = "Those lights have been back there since the turn off." },
            new Beat { AtMetres = 2800f, NeedsFollower = true,
                Line = "Anyone can be going the same way. It is one road." }
        };

        private int next;
        private int hint = -1;

        private void Awake()
        {
            if (path == null) path = FindObjectOfType<RoadPath>();
            if (car == null) car = FindObjectOfType<CarDriver>();
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();
            if (director == null) director = FindObjectOfType<NightRoadDirector>();
        }

        private void Update()
        {
            if (next >= beats.Length || path == null || car == null) return;

            // Com o motor morto o capitulo passa a ser outro: o que ha para pensar
            // ca fora e do `NightRoadDirector`, nao daqui.
            if (!car.EngineRunning) { next = beats.Length; return; }

            float here = path.DistanceOf(car.transform.position, ref hint);
            var beat = beats[next];
            if (here < beat.AtMetres) return;

            next++;
            if (beat.NeedsFollower && (director == null || !director.FollowerVisible)) return;
            if (string.IsNullOrWhiteSpace(beat.Line)) return;

            thoughts?.Think($"road_{next}", beat.Line, 3, true, 3.6f);
        }
    }
}
