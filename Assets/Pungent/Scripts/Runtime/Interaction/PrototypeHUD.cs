using UnityEngine;

namespace Pungent.Interaction
{
    [DisallowMultipleComponent]
    public sealed class PrototypeHUD : MonoBehaviour
    {
        [SerializeField] private string objective = "OBJECTIVE: Restart the router at the end of the hall";
        private string sideObjective;
        private bool notice;
        private string noticeText = "Check the phone  [TAB]";
        private string prompt;
        private string message;
        private float messageUntil;
        private GUIStyle objectiveStyle;
        private GUIStyle sideObjectiveStyle;
        private GUIStyle noticeStyle;
        private GUIStyle promptStyle;
        private GUIStyle messageStyle;

        [Header("Reticulo")]
        [Tooltip("Tamanho do ponto em repouso, em pixeis.")]
        [SerializeField, Min(2f)] private float dotSize = 6f;

        [Tooltip("Tamanho do ponto quando ha algo interagivel a frente.\n\n"
               + "**Ligeiramente maior, e mais nada.** Havia aqui um anel e uma "
               + "linha de texto a dizer o que ia acontecer; os dois liam-se como "
               + "um jogo a explicar-se. Um ponto que engorda dois pixeis diz "
               + "exactamente a mesma coisa — isto responde — sem tirar os olhos "
               + "do sitio nem escrever por cima da casa.\n\n"
               + "Nao subir muito: passados uns doze pixeis deixa de se ler como "
               + "o mesmo ponto a reagir e passa a ser outro simbolo a aparecer, "
               + "que e o anel outra vez com outro nome.")]
        [SerializeField, Min(2f)] private float dotSizeActive = 10f;

        [Tooltip("Quanto o ponto ganha de brilho sobre o que e interagivel. O "
               + "tamanho e quem faz o trabalho; isto so o impede de parecer "
               + "apagado quando cresce.")]
        [SerializeField, Range(0f, 1f)] private float activeBrightness = 0.35f;

        [SerializeField, Min(1f)] private float reticleSpeed = 11f;

        private bool reticleHidden;
        private Texture2D dotTexture;
        private float reticleBlend;

        public void SetPrompt(string value) => prompt = value;
        public void SetObjective(string value) => objective = value;

        /// <summary>
        /// Objetivo secundario, por baixo do principal e mais apagado. Serve o que
        /// o jogador pode fazer e nao tem de fazer: sem isto, uma tarefa opcional
        /// no apartamento nunca chega a existir para quem nao tropeca nela.
        /// </summary>
        public void SetSideObjective(string value) => sideObjective = value;

        /// <summary>
        /// O aviso de mensagem por abrir, por baixo do objectivo.
        ///
        /// **Existe porque o telemovel deste jogo nao se ouve.** As mensagens chegam
        /// com dezenas de segundos de atraso e por vezes durante uma tarefa; o
        /// jogador nao tem maneira nenhuma de saber que ha alguma coisa la dentro
        /// sem a abrir de vez em quando a ver. Um fio inteiro do Pai pode passar sem
        /// ser lido, e e esse fio que decide um dos cinco finais.
        ///
        /// Quem o liga e o <see cref="PrototypePhoneUI"/>, que e quem sabe se ha
        /// nao-lidas e se o telemovel esta aberto.
        /// </summary>
        public void SetNotice(bool value, string text = "Check the phone  [TAB]")
        {
            notice = value;
            noticeText = text;
        }

        public void ShowMessage(string value, float seconds = 3f)
        {
            message = value;
            messageUntil = Time.time + seconds;
        }

        /// <summary>
        /// A barra de uma tarefa domestica a decorrer.
        ///
        /// ---
        ///
        /// **Uma tarefa que prende o jogador no sitio tem de dizer que o esta a
        /// fazer.** Elas duram entre onze e vinte segundos, suspendem o movimento, e
        /// ate aqui o unico sinal de que alguma coisa estava a acontecer eram os
        /// pensamentos — que saem espacados e podem nem sair, porque o
        /// `PlayerThoughtDirector` desconta banalidades quando a casa falou ha pouco.
        /// Quem apanhasse esse caso ficava quinze segundos preso a olhar para um
        /// lava-loica sem saber se tinha carregado no botao ou se o jogo tinha
        /// bloqueado.
        ///
        /// O trabalho ja estava feito: a `HomeTaskInteractable` calcula o progresso
        /// para alimentar o objecto que vai na mao. So nao havia quem o mostrasse.
        ///
        /// **Ao pe do prompt e nao no cimo do ecra.** E onde o jogador estava a olhar
        /// no instante em que carregou.
        /// </summary>
        public void SetTaskProgress(string label, float normalised)
        {
            taskLabel = label;
            taskProgress = Mathf.Clamp01(normalised);
            taskActive = !string.IsNullOrEmpty(label);
        }

        public void ClearTaskProgress()
        {
            taskActive = false;
            taskLabel = null;
        }

        private bool taskActive;
        private string taskLabel;
        private float taskProgress;
        private GUIStyle taskStyle;
        private Texture2D barTexture;

private void EnsureStyles()
        {
            if (objectiveStyle != null) return;
            objectiveStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.88f, 0.9f, 0.86f) }
            };
            // Mais pequeno e mais apagado que o principal: le-se como oferta, nao
            // como ordem.
            sideObjectiveStyle = new GUIStyle(objectiveStyle)
            {
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                normal = { textColor = new Color(0.72f, 0.74f, 0.70f, 0.82f) }
            };
            // Cor propria, e nao a do objectivo: isto nao e uma ordem da historia.
            noticeStyle = new GUIStyle(sideObjectiveStyle)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.86f, 0.82f, 0.62f, 1f) }
            };
            promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            messageStyle = new GUIStyle(promptStyle)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                alignment = TextAnchor.UpperCenter
            };
            taskStyle = new GUIStyle(promptStyle)
            {
                fontSize = 15,
                normal = { textColor = new Color(0.86f, 0.88f, 0.84f, 0.95f) }
            };
        }

        /// <summary>
        /// A barra: um risco fino, sem moldura e sem numeros.
        ///
        /// Nao ha percentagem escrita de propósito. O que o jogador precisa de saber e
        /// "isto esta a andar e falta pouco", e um numero convida a olhar para ele em
        /// vez de para a casa — que e onde as coisas acontecem enquanto ele esta preso
        /// no sitio.
        /// </summary>
        private void DrawTaskBar(float width, float height)
        {
            if (barTexture == null)
            {
                barTexture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                barTexture.SetPixel(0, 0, Color.white);
                barTexture.Apply();
            }

            const float BarWidth = 260f, BarHeight = 4f;
            float x = width * 0.5f - BarWidth * 0.5f;
            float y = height * 0.66f + 34f;

            if (!string.IsNullOrEmpty(taskLabel))
                ShadowedLabel(new Rect(width * 0.5f - 280f, y - 30f, 560f, 26f), taskLabel, taskStyle);

            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(x - 1f, y - 1f, BarWidth + 2f, BarHeight + 2f), barTexture);

            GUI.color = new Color(1f, 1f, 1f, 0.18f);
            GUI.DrawTexture(new Rect(x, y, BarWidth, BarHeight), barTexture);

            GUI.color = new Color(0.88f, 0.90f, 0.86f, 0.92f);
            GUI.DrawTexture(new Rect(x, y, BarWidth * taskProgress, BarHeight), barTexture);

            GUI.color = Color.white;
        }

        private void Update()
        {
            // O prompt so tem conteudo quando o interactor tem um alvo valido,
            // por isso serve de sinal para o reticulo sem acoplar os dois sistemas.
            bool hasTarget = !string.IsNullOrEmpty(prompt);
            reticleBlend = Mathf.MoveTowards(reticleBlend, hasTarget ? 1f : 0f,
                Time.unscaledDeltaTime * reticleSpeed);
        }

        private void OnDestroy()
        {
            if (dotTexture != null) Destroy(dotTexture);
            if (barTexture != null) Destroy(barTexture);
        }

        /// <summary>
        /// O disco do ponto. Gerado a 32 e nao a 16: e o mesmo disco esticado dos
        /// seis aos dez pixeis, e uma textura pequena a crescer perde a borda
        /// suave — via-se o ponto a ficar quadrado no instante em que expande, que
        /// e precisamente o instante em que se esta a olhar para ele.
        /// </summary>
        private void EnsureReticleTextures()
        {
            if (dotTexture == null)
                dotTexture = CreateCircleTexture(32, 11f, 0f);
        }

        /// <summary>Disco ou anel suavizado, gerado uma vez em memoria.</summary>
        private static Texture2D CreateCircleTexture(int size, float outerRadius, float innerRadius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            float centre = (size - 1) * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - centre;
                    float dy = y - centre;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha = Mathf.Clamp01(outerRadius - distance);
                    if (innerRadius > 0f)
                        alpha *= Mathf.Clamp01(distance - innerRadius);

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static void DrawReticle(Texture2D texture, float size, float alpha, float width, float height)
        {
            if (texture == null || alpha <= 0.002f) return;

            var rect = new Rect(width * 0.5f - size * 0.5f, height * 0.5f - size * 0.5f, size, size);

            // Contorno escuro, para o reticulo nao desaparecer sobre paredes claras.
            GUI.color = new Color(0f, 0f, 0f, alpha * 0.55f);
            GUI.DrawTexture(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f), texture);

            GUI.color = new Color(1f, 1f, 1f, alpha * 0.92f);
            GUI.DrawTexture(rect, texture);
            GUI.color = Color.white;
        }

        /// <summary>
        /// Desenha o texto com uma sombra em vez de o assentar numa caixa escura.
        /// A legibilidade vem do contorno, nao de paineis.
        /// </summary>
        private static void ShadowedLabel(Rect rect, string text, GUIStyle style)
        {
            Color original = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
            style.normal.textColor = original;
            GUI.Label(rect, text, style);
        }

        /// <summary>Esconde a mira. Usado enquanto ele conduz (ver `CarSeat`).</summary>
        public void SetReticleHidden(bool hidden) => reticleHidden = hidden;

        private void OnGUI()
        {
            EnsureStyles();
            float width = Screen.width;
            float height = Screen.height;

            EnsureReticleTextures();

            // **O mesmo ponto o tempo todo, um bocadinho maior sobre o que responde.**
            //
            // Nao ha troca de simbolo nem texto nenhum. Um ponto que se transforma
            // noutra coisa obriga a olhar para ele para perceber o que mudou; um
            // ponto que cresce dois pixeis e lido pela visao periferica sem sair do
            // objecto — que e onde a atencao deve estar num jogo em que o que
            // interessa acontece atras de ti.
            //
            // Ao volante o ponto desaparece **enquanto nao ha nada**: quatro minutos
            // com um ponto no para-brisas leem-se como sujidade no ecra. Assim que
            // aparece alguma coisa a que se possa mexer — o radio do tablier — ele
            // volta, ja crescido. Esconder a mira nao pode significar esconder que
            // ha ali uma interaccao.
            float dot = Mathf.Lerp(dotSize, dotSizeActive, reticleBlend);
            float dotAlpha = (reticleHidden ? reticleBlend : 1f)
                           * (1f - activeBrightness + activeBrightness * (1f + reticleBlend) * 0.5f);

            DrawReticle(dotTexture, dot, Mathf.Clamp01(dotAlpha), width, height);

            if (!string.IsNullOrEmpty(objective))
                ShadowedLabel(new Rect(32f, 26f, width - 64f, 35f), objective, objectiveStyle);

            if (!string.IsNullOrEmpty(sideObjective))
                ShadowedLabel(new Rect(32f, 52f, width - 64f, 30f), sideObjective, sideObjectiveStyle);

            // A mensagem por abrir, por baixo do objectivo e por baixo da linha
            // secundaria — nunca por cima de nenhum dos dois. E informacao do
            // jogador e nao da historia: nao empurra o que ele tem de fazer.
            //
            // Pisca devagar, e nunca chega a apagar-se de todo. Um aviso que
            // desaparece obriga a olhar no momento certo; este so respira.
            if (notice)
            {
                float breath = 0.72f + 0.28f * Mathf.Sin(Time.unscaledTime * 2.2f);
                var was = noticeStyle.normal.textColor;
                noticeStyle.normal.textColor = new Color(was.r, was.g, was.b, breath);
                ShadowedLabel(new Rect(32f, 76f, width - 64f, 28f), noticeText, noticeStyle);
                noticeStyle.normal.textColor = was;
            }

            // O `prompt` continua a chegar e continua a contar: e ele que diz se ha
            // alvo, e e dele que sai a expansao do ponto no `Update`. So nao e
            // escrito. Quem o manda nao precisa de saber disto.

            if (taskActive) DrawTaskBar(width, height);

            if (!string.IsNullOrEmpty(message) && Time.time < messageUntil)
                ShadowedLabel(new Rect(width * 0.5f - 360f, height * 0.2f, 720f, 120f), message, messageStyle);
        }
    }
}
