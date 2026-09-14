using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// O movel muda de aspecto enquanto a tarefa decorre.
    ///
    /// ---
    ///
    /// **A barra diz que esta a andar; isto diz que esta a acontecer.** Sao coisas
    /// diferentes e as duas sao precisas. Uma barra e informacao sobre o jogo; a loica
    /// a ficar limpa e informacao sobre o mundo, e e a segunda que faz o gesto parecer
    /// um gesto em vez de um temporizador.
    ///
    /// Interpola uma cor do inicio para o fim ao longo da tarefa: o lava-loica
    /// encardido a clarear, o fogao a aquecer, a maquina a acender. Um valor, uma
    /// curva, nada de particulas.
    ///
    /// ---
    ///
    /// **Sobre uma instancia do material e nao sobre o asset partilhado.** Escrever no
    /// `sharedMaterial` muda o ficheiro em disco: o lava-loica ficaria lavado no
    /// projecto para sempre, e o Dia 3 comecava com a tarefa ja feita. O `Renderer`
    /// devolve uma copia propria a primeira vez que se lhe pede `material`, e e nessa
    /// que se escreve — mas so quando a tarefa comeca, para nao criar copias em cada
    /// movel da casa no arranque.
    ///
    /// Reposto no fim se <see cref="keepsFinalLook"/> estiver desligado. A loica fica
    /// lavada; o fogao volta a arrefecer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TaskProgressTint : MonoBehaviour, ITaskProgressVisual
    {
        [Tooltip("O que muda de cor. Vazio = os renderers deste objecto e filhos.")]
        [SerializeField] private Renderer[] targets = new Renderer[0];

        [Tooltip("Propriedade de cor do shader. `_BaseColor` em URP; `_Color` no "
               + "Built-in e em alguns materiais importados.")]
        [SerializeField] private string colourProperty = "_BaseColor";

        [Tooltip("Como esta antes de a tarefa comecar. Vazio (alfa 0) = usa a cor que "
               + "o material ja tem, que e o caso normal.")]
        [SerializeField] private Color from = new Color(0f, 0f, 0f, 0f);

        [SerializeField] private Color to = Color.white;

        [Tooltip("A forma da mudanca. Por omissao devagar ao principio e a acabar de "
               + "repente — que e como se lava loica.")]
        [SerializeField] private AnimationCurve shape = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Fica assim depois de acabar. Ligado para o que nao volta atras (a "
               + "loica lavada); desligado para o que volta (o fogao a arrefecer).")]
        [SerializeField] private bool keepsFinalLook = true;

        private Material[] instances;
        private Color[] originals;
        private int propertyId = -1;

        public void BeginTask()
        {
            if (propertyId < 0) propertyId = Shader.PropertyToID(colourProperty);

            Renderer[] list = targets != null && targets.Length > 0
                ? targets
                : GetComponentsInChildren<Renderer>(true);

            instances = new Material[list.Length];
            originals = new Color[list.Length];

            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] == null) continue;

                // `.material` e nao `.sharedMaterial`: ver a nota da classe.
                instances[i] = list[i].material;
                if (instances[i] == null) continue;

                originals[i] = instances[i].HasProperty(propertyId)
                    ? instances[i].GetColor(propertyId)
                    : Color.white;
            }

            SetTaskProgress(0f);
        }

        public void SetTaskProgress(float normalised)
        {
            if (instances == null) return;

            float k = shape != null ? shape.Evaluate(Mathf.Clamp01(normalised))
                                    : Mathf.Clamp01(normalised);

            for (int i = 0; i < instances.Length; i++)
            {
                if (instances[i] == null || !instances[i].HasProperty(propertyId)) continue;

                // Alfa zero na cor de partida quer dizer "comeca como ja estava".
                Color start = from.a <= 0f ? originals[i] : from;
                instances[i].SetColor(propertyId, Color.Lerp(start, to, k));
            }
        }

        public void EndTask()
        {
            if (instances == null) return;

            if (!keepsFinalLook)
                for (int i = 0; i < instances.Length; i++)
                    if (instances[i] != null && instances[i].HasProperty(propertyId))
                        instances[i].SetColor(propertyId, originals[i]);

            instances = null;
            originals = null;
        }

#if UNITY_EDITOR
        /// <summary>Usado pelas ferramentas de ligacao, que vivem noutra assembly.</summary>
        public void EditorConfigure(Renderer[] what, Color target, string property = "_BaseColor",
            bool keeps = true)
        {
            targets = what;
            to = target;
            colourProperty = property;
            keepsFinalLook = keeps;
        }
#endif
    }
}
