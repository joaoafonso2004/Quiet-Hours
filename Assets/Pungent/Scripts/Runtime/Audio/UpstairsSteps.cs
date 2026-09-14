using Pungent.Narrative;
using UnityEngine;

namespace Pungent.Audio
{
    /// <summary>
    /// Alguem no andar de cima que anda quando tu andas.
    ///
    /// ---
    ///
    /// **O truque mais barato do genero, e o unico que nao precisa de arte nenhuma.**
    ///
    /// Um clip de passo, um ponto tres metros acima da cabeca, e uma regra: comeca
    /// quando o jogador comeca e **acaba um passo depois de ele acabar**. Esse passo
    /// a mais e o componente inteiro. Sem ele, ouvem-se passos por cima — um vizinho.
    /// Com ele, o jogador para a meio do corredor, ouve mais um passo por cima da
    /// cabeca, e a partir dai anda de outra maneira.
    ///
    /// Ninguem comenta. Nao ha objectivo, nao ha HUD, nao ha pensamento. O predio tem
    /// tres andares e alguem mora la em cima: e verdade, e continua a ser verdade
    /// depois de o jogador ter percebido.
    ///
    /// ---
    ///
    /// **As duas maneiras de o fazer**, e sao dias diferentes:
    ///
    /// - <see cref="Mode.Mirrors"/> — anda quando tu andas. E a primeira vez. Le-se
    ///   como coincidencia durante os primeiros dez segundos e como outra coisa a
    ///   partir dai.
    /// - <see cref="Mode.Answers"/> — anda **so quando tu paras**. E a segunda vez, e
    ///   nao e a mesma ideia repetida: e a resposta. Quem anda por cima esta a esperar
    ///   que tu deixes de fazer barulho para poder ouvir melhor.
    ///
    /// ---
    ///
    /// **Acaba-se sozinho.** <see cref="runs"/> conta quantas vezes isto pode
    /// acontecer, e depois cala-se para sempre. Um efeito que se repete a noite toda
    /// deixa de ser uma pessoa e passa a ser um sistema, e o jogador comeca a andar
    /// aos bocados de proposito para o testar — que e exactamente o oposto.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class UpstairsSteps : MonoBehaviour
    {
        public enum Mode
        {
            /// <summary>Anda enquanto o jogador anda, e da mais um passo depois.</summary>
            Mirrors,
            /// <summary>Anda so enquanto o jogador esta parado.</summary>
            Answers
        }

        [SerializeField] private Mode mode = Mode.Mirrors;

        [Tooltip("Os passos, um por ficheiro. Varios, para nao se ouvir o mesmo duas "
               + "vezes seguidas — e o que denuncia um clip a repetir.")]
        [SerializeField] private AudioClip[] steps = new AudioClip[0];

        [Tooltip("Alternativa: uma gravacao corrida de alguem a andar, em ciclo.\n\n"
               + "Quando esta preenchida ganha aos passos soltos. E o formato mais "
               + "facil de gravar — trinta segundos a andar em cima de uma laje — e "
               + "e mais convincente do que disparos discretos, porque a cadencia e "
               + "de uma pessoa a serio e nao de um temporizador.\n\n"
               + "A regra do componente nao muda: comeca quando o jogador comeca, e "
               + "**para um pouco depois de ele parar**. E esse atraso, e nao os "
               + "passos, que faz o trabalho todo.")]
        [SerializeField] private AudioClip walkLoop;

        [Tooltip("Para o ciclo: quanto tempo continua a andar depois de o jogador "
               + "parar. O equivalente ao passo a mais.")]
        [SerializeField, Range(0.15f, 1.5f)] private float overrunSeconds = 0.55f;

        [Tooltip("Para o ciclo: quanto demora a esmorecer. Curto — quem para de andar "
               + "para de andar, nao se afasta.")]
        [SerializeField, Range(0.02f, 0.6f)] private float loopFade = 0.18f;

        [Header("Janela")]
        [Tooltip("So depois deste acontecimento.")]
        [SerializeField] private string armOnEvent;

        [Tooltip("Cala-se de vez depois deste.")]
        [SerializeField] private string silencedByEvent;

        [Tooltip("Quantas vezes isto pode acontecer antes de se calar para sempre. "
               + "Duas ou tres. Ver a nota da classe.")]
        [SerializeField, Min(1)] private int runs = 2;

        [Tooltip("Silencio minimo entre duas vezes. Sem isto, parar e recomecar a "
               + "andar gastava as duas seguidas.")]
        [SerializeField, Min(2f)] private float restBetweenRuns = 45f;

        [Header("Onde")]
        [Tooltip("Altura acima da cabeca do jogador. Tres metros e o pe-direito de um "
               + "andar; mais alto le-se como telhado e menos le-se como a mesma sala.")]
        [SerializeField, Min(1f)] private float ceilingHeight = 3.1f;

        [Tooltip("Atraso com que quem esta la em cima acompanha o jogador. A zero, a "
               + "fonte fica colada a cabeca dele e le-se como um erro de som e nao "
               + "como uma pessoa.")]
        [SerializeField, Min(0f)] private float followLag = 1.1f;

        [Header("Passada")]
        [Tooltip("Segundos entre passos. Mais lenta do que a do jogador de proposito: "
               + "a mesma cadencia le-se como eco dos passos dele.")]
        [SerializeField, Min(0.15f)] private float stepInterval = 0.66f;

        [Tooltip("Quanto a cadencia varia. Um metronomo nao e uma pessoa.")]
        [SerializeField, Range(0f, 0.3f)] private float intervalJitter = 0.09f;

        [Tooltip("Velocidade a partir da qual o jogador conta como estando a andar.")]
        [SerializeField, Min(0.05f)] private float walkingSpeed = 0.6f;

        [Header("Mirrors")]
        [Tooltip("Passos dados **depois** de o jogador parar. E o componente todo. "
               + "Um. Dois ja e uma perseguicao e este nao e o dia disso.")]
        [SerializeField, Range(0, 3)] private int overrunSteps = 1;

        [Tooltip("Passos que tem de ter sido dados para isto contar como uma vez. "
               + "Atravessar meio corredor nao gasta uma das duas oportunidades.")]
        [SerializeField, Min(1)] private int minimumSteps = 4;

        [Header("Answers")]
        [Tooltip("Quanto tempo o jogador tem de estar parado antes de comecarem.")]
        [SerializeField, Min(0.2f)] private float answerAfter = 1.4f;

        [Tooltip("Quantos passos dao antes de pararem por si.")]
        [SerializeField, Min(1)] private int answerSteps = 4;

        [Header("Som")]
        [SerializeField, Range(0f, 1f)] private float volume = 0.5f;
        [SerializeField] private Vector2 pitchRange = new Vector2(0.88f, 0.98f);

        [SerializeField] private ChapterDirector director;

        private AudioSource source;
        private Transform player;
        private CharacterController body;

        private Vector3 followed;
        private bool placed;

        private int spentRuns;
        private float lastRunEndedAt = -999f;

        private bool walking;
        private float stillSince;
        private float nextStepAt;
        private int stepsThisRun;
        private int overrunLeft;
        private bool runCounted;

        /// <summary>Ja gastou tudo o que tinha. Util para inspeccionar.</summary>
        public bool Spent => spentRuns >= runs;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.dopplerLevel = 0f;
            if (source.maxDistance < 12f) source.maxDistance = 14f;
        }

        /// <summary>Ha alguma coisa para tocar: passos soltos ou a gravacao corrida.</summary>
        private bool HasSound => walkLoop != null || (steps != null && steps.Length > 0);

        private void Update()
        {
            if (Spent || !HasSound) return;
            if (!Armed()) { FadeLoop(false); return; }
            if (!ResolvePlayer()) return;

            Track();

            bool moving = Speed() >= walkingSpeed;
            if (moving) stillSince = -1f;
            else if (stillSince < 0f) stillSince = Time.time;

            if (mode == Mode.Mirrors) UpdateMirrors(moving);
            else UpdateAnswers(moving);

            FadeLoop(loopWanted);
        }

        /// <summary>
        /// Sobe e desce o ciclo da gravacao corrida.
        ///
        /// Cortar a seco denunciava o truque: um som que desaparece num frame le-se
        /// como um ficheiro a parar, e nao como uma pessoa a deixar de andar. Dois
        /// decimos chegam para soar a passada que acaba.
        /// </summary>
        private void FadeLoop(bool wanted)
        {
            if (walkLoop == null) return;

            float target = wanted ? volume : 0f;
            source.volume = Mathf.MoveTowards(source.volume, target,
                Time.deltaTime * volume / Mathf.Max(0.02f, loopFade));

            if (wanted && !source.isPlaying)
            {
                source.clip = walkLoop;
                source.loop = true;
                source.pitch = Random.Range(pitchRange.x, pitchRange.y);
                // De um ponto ao acaso: duas vezes seguidas a comecar no mesmo sitio
                // da gravacao leem-se como o mesmo ficheiro, que e o que se quer
                // esconder.
                source.time = Random.Range(0f, Mathf.Max(0.1f, walkLoop.length - 0.5f));
                source.Play();
            }
            else if (!wanted && source.isPlaying && source.volume <= 0.001f)
            {
                source.Stop();
            }
        }

        private bool loopWanted;

        // ------------------------------------------------------------------

        /// <summary>
        /// Anda quando ele anda, e da mais um passo depois de ele parar.
        /// </summary>
        private void UpdateMirrors(bool moving)
        {
            if (moving)
            {
                if (!walking)
                {
                    // Um comeco novo so conta se ja tiver passado descanso suficiente
                    // desde o anterior.
                    if (Time.time - lastRunEndedAt < restBetweenRuns) return;

                    walking = true;
                    stepsThisRun = 0;
                    overrunLeft = overrunSteps;
                    runCounted = false;
                    // Meio compasso de atraso no arranque: comecar no mesmo frame em
                    // que ele comeca a andar le-se como um som do proprio jogador.
                    nextStepAt = Time.time + stepInterval * 0.5f;
                }

                TickStep();
                return;
            }

            if (!walking) return;

            // Ele parou. **O passo a mais**, que e o componente inteiro.
            //
            // Com a gravacao corrida nao ha passos a contar: continua a andar mais
            // meio segundo e so depois esmorece. E a mesma coisa dita de outra
            // maneira — quem esta em cima acabou a passada depois de tu acabares a
            // tua.
            if (walkLoop != null)
            {
                if (stillSince >= 0f && Time.time - stillSince < overrunSeconds)
                {
                    loopWanted = true;
                    return;
                }
                EndRun();
                return;
            }

            if (overrunLeft > 0)
            {
                if (Time.time >= nextStepAt)
                {
                    Step();
                    overrunLeft--;
                    nextStepAt = Time.time + Interval();
                }
                return;
            }

            EndRun();
        }

        /// <summary>
        /// Anda so quando ele para. Alguem a espera de silencio para poder ouvir.
        /// </summary>
        private void UpdateAnswers(bool moving)
        {
            if (moving)
            {
                if (walking) EndRun();
                return;
            }

            if (stillSince < 0f) return;
            if (Time.time - stillSince < answerAfter) return;

            if (!walking)
            {
                if (Time.time - lastRunEndedAt < restBetweenRuns) return;

                walking = true;
                stepsThisRun = 0;
                runCounted = false;
                nextStepAt = Time.time;
            }

            if (stepsThisRun >= answerSteps) { EndRun(); return; }
            TickStep();
        }

        private void TickStep()
        {
            // Com a gravacao corrida nao ha passos a disparar: basta o ciclo estar a
            // tocar. O `stepsThisRun` continua a contar tempo em vez de passos, para
            // o `minimumSteps` e o `runCounted` funcionarem como antes.
            if (walkLoop != null)
            {
                loopWanted = true;
                if (Time.time >= nextStepAt)
                {
                    stepsThisRun++;
                    if (!runCounted && stepsThisRun >= minimumSteps) runCounted = true;
                    nextStepAt = Time.time + Interval();
                }
                return;
            }

            if (Time.time < nextStepAt) return;
            Step();
            nextStepAt = Time.time + Interval();
        }

        private void Step()
        {
            stepsThisRun++;
            if (!runCounted && stepsThisRun >= minimumSteps) runCounted = true;

            var clip = steps[Random.Range(0, steps.Length)];
            if (clip == null) return;

            source.pitch = Random.Range(pitchRange.x, pitchRange.y);
            source.PlayOneShot(clip, volume);
        }

        private void EndRun()
        {
            walking = false;
            loopWanted = false;
            lastRunEndedAt = Time.time;

            // Uma travessia curta nao gasta nenhuma das duas oportunidades. E o que
            // permite por isto no corredor sem ter medo de o jogador o gastar a
            // atravessar a casa de banho.
            if (runCounted) spentRuns++;
            runCounted = false;
        }

        private float Interval() =>
            Mathf.Max(0.12f, stepInterval + Random.Range(-intervalJitter, intervalJitter));

        // ------------------------------------------------------------------

        /// <summary>
        /// A fonte segue o jogador por cima, com atraso.
        ///
        /// Nao e "onde ele esta", e "onde ele estava ha um segundo": e a diferenca
        /// entre uma pessoa a andar por cima e um som pendurado na cabeca do jogador.
        /// A altura e absoluta e nao segue os saltos nem a inclinacao — o chao de cima
        /// esta onde esta.
        /// </summary>
        private void Track()
        {
            Vector3 above = new Vector3(player.position.x,
                                        player.position.y + ceilingHeight,
                                        player.position.z);

            if (!placed) { followed = above; placed = true; }
            else
            {
                float t = followLag <= 0f ? 1f
                                          : 1f - Mathf.Exp(-Time.deltaTime / followLag);
                followed = Vector3.Lerp(followed, above, t);
                followed.y = above.y;
            }

            transform.position = followed;
        }

        private float Speed()
        {
            if (body == null) return 0f;
            Vector3 v = body.velocity;
            v.y = 0f;
            return v.magnitude;
        }

        private bool ResolvePlayer()
        {
            if (player != null && body != null) return true;

            var motor = FindObjectOfType<Pungent.Player.PlayerMotor>();
            if (motor == null) return false;

            player = motor.transform;
            body = motor.GetComponent<CharacterController>();
            return body != null;
        }

        private bool Armed()
        {
            bool gated = !string.IsNullOrWhiteSpace(armOnEvent)
                      || !string.IsNullOrWhiteSpace(silencedByEvent);
            if (!gated) return true;

            director = ChapterDirector.Resolve(director);
            if (director == null) return false;

            if (!string.IsNullOrWhiteSpace(armOnEvent) && !director.HasSeen(armOnEvent))
                return false;
            if (!string.IsNullOrWhiteSpace(silencedByEvent) && director.HasSeen(silencedByEvent))
                return false;

            return true;
        }

#if UNITY_EDITOR
        /// <summary>Usado pelas ferramentas de ligacao, que vivem noutra assembly.</summary>
        public void EditorConfigure(Mode how, AudioClip[] clips, string arms, string silencedBy,
            int howManyRuns = 2, float rest = 45f, float height = 3.1f, float loudness = 0.5f,
            AudioClip loop = null)
        {
            mode = how;
            steps = clips;
            walkLoop = loop;
            armOnEvent = arms;
            silencedByEvent = silencedBy;
            runs = howManyRuns;
            restBetweenRuns = rest;
            ceilingHeight = height;
            volume = loudness;
        }
#endif
    }
}
