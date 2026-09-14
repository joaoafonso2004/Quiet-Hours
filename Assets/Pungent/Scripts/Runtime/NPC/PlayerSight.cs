using UnityEngine;

namespace Pungent.NPC
{
    /// <summary>
    /// "O jogador esta mesmo a ver isto?" — uma pergunta so, respondida num sitio so.
    ///
    /// Existe porque a resposta ingenua esta errada de duas maneiras opostas, e os
    /// dois sistemas que fazem a pergunta partiam-se de maneiras diferentes.
    ///
    /// **O angulo sozinho mente por excesso.** `Vector3.Angle(camera.forward, ...)`
    /// da verdadeiro para uma pessoa encostada do outro lado de uma parede: esta na
    /// direccao para onde olhas, e nao a ves. Um Rui que se esconde disso esconde-se
    /// de coisas que ninguem podia ver, e o susto da camara disparava atraves de
    /// paredes.
    ///
    /// **O raio ao centro do corpo mente por defeito.** Uma pessoa meio tapada por
    /// uma ombreira tem o centro do peito atras da madeira e a cara a vista. Um raio
    /// so ao centro diz "nao se ve" com ele a olhar para o jogador.
    ///
    /// Entao: **frustum primeiro** (barato, e elimina quase tudo), e so depois um
    /// punhado de raios a pontos diferentes do corpo. Basta um chegar.
    /// </summary>
    public static class PlayerSight
    {
        /// <summary>
        /// Alturas onde se procura linha de vista, em fraccao da altura do alvo.
        /// A cabeca primeiro: e a que aparece antes do resto quando alguem espreita
        /// de uma esquina, e e a que faz o jogador duvidar.
        /// </summary>
        private static readonly float[] SampleHeights = { 0.92f, 0.70f, 0.45f };

        private static readonly RaycastHit[] Buffer = new RaycastHit[16];

        /// <summary>
        /// Ha alguma coisa **que nao seja o proprio alvo** entre os dois pontos?
        ///
        /// A distincao nao e um pormenor: era um defeito com sintoma. Os pontos de
        /// amostra vivem no eixo do corpo, e o corpo tem uma capsula a volta desse
        /// eixo com uns trinta centimetros de raio. Um `Physics.Raycast` simples
        /// batia na capsula **do proprio Rui** antes de chegar a cabeca dele e
        /// declarava-o tapado — medido, ele estava a onze graus do centro do ecra,
        /// a vista, e o teste dizia que nao se via.
        ///
        /// Uma pessoa nunca se tapa a si propria. Os colisores dela sao saltados.
        /// </summary>
        private static bool Blocked(Vector3 from, Vector3 to, Transform ignoreRoot,
            LayerMask blockers, Transform alsoIgnoreRoot = null)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.05f) return false;

            int count = Physics.RaycastNonAlloc(from, delta / distance, Buffer,
                distance, blockers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider collider = Buffer[i].collider;
                if (collider == null) continue;
                if (ignoreRoot != null && collider.transform.IsChildOf(ignoreRoot)) continue;
                if (alsoIgnoreRoot != null && collider.transform.IsChildOf(alsoIgnoreRoot)) continue;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Verdadeiro se <paramref name="target"/> esta dentro do campo de visao e
        /// sem nada solido pelo meio.
        /// </summary>
        /// <param name="margin">
        /// Encolhe o rectangulo do ecra que conta como "a ver". Zero usa o ecra
        /// todo; 0.1 ignora os 10% de cada bordo.
        ///
        /// Para quem se esconde isto quer-se **negativo ou zero**: ele deve reagir
        /// a estar na periferia, que e onde e apanhado de raspao, e nao so quando
        /// ja esta no meio do ecra — nessa altura o jogador ja o viu bem e esconder
        /// -se nao engana ninguem.
        /// </param>
        public static bool CanPlayerSee(Camera camera, Transform target, float height,
            LayerMask blockers, float margin = 0f, float maxDistance = 40f)
        {
            if (camera == null || target == null) return false;

            Vector3 basePoint = target.position;
            Vector3 eye = camera.transform.position;

            if ((basePoint - eye).sqrMagnitude > maxDistance * maxDistance) return false;

            for (int i = 0; i < SampleHeights.Length; i++)
            {
                Vector3 point = basePoint + Vector3.up * (height * SampleHeights[i]);

                Vector3 viewport = camera.WorldToViewportPoint(point);
                if (viewport.z <= 0f) continue;                      // atras da camara
                if (viewport.x < margin || viewport.x > 1f - margin) continue;
                if (viewport.y < margin || viewport.y > 1f - margin) continue;

                // `QueryTriggerInteraction.Ignore`, la dentro: os volumes de
                // historia e as ajudas de mira nao sao paredes e nao tapam ninguem.
                if (!Blocked(eye, point, target, blockers))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// **So paredes.** Ha caminho livre ate ao alvo, esteja ele no ecra ou nao.
        ///
        /// A distincao entre isto e o <see cref="CanPlayerSee"/> nao e académica —
        /// custou o susto do quarto do Rui. A regra pedida era "o camera lock nunca
        /// dispara atraves de paredes", e foi implementada com o teste completo, que
        /// exige tambem que o alvo esteja **dentro do enquadramento**. So que quem
        /// entra no quarto ainda nao esta a olhar para ele: e a camara que se vira,
        /// e esse e o efeito todo. Com o teste completo o evento so podia disparar
        /// quando ja nao fazia falta, e por isso deixou de disparar de todo.
        ///
        /// Quem quer saber "ele esta a ser visto?" — o <see cref="RuiPeeking"/>, que
        /// se esconde de ser apanhado — usa o outro. Quem quer saber "ha parede pelo
        /// meio?" usa este.
        /// </summary>
        public static bool HasLineOfSight(Vector3 eye, Transform target, float height,
            LayerMask blockers, float maxDistance = 40f)
            => HasLineOfSight(eye, target, height, blockers, null, maxDistance);

        /// <summary>
        /// Variante que tambem ignora o corpo de quem esta a olhar. A camara do
        /// jogador vive dentro do CharacterController; um raio que saia ligeiramente
        /// para baixo pode apanhar essa capsula antes de chegar ao alvo.
        /// </summary>
        public static bool HasLineOfSight(Vector3 eye, Transform target, float height,
            LayerMask blockers, Transform observerRoot, float maxDistance = 40f)
        {
            if (target == null) return false;
            if ((target.position - eye).sqrMagnitude > maxDistance * maxDistance) return false;

            for (int i = 0; i < SampleHeights.Length; i++)
            {
                Vector3 point = target.position + Vector3.up * (height * SampleHeights[i]);
                if (!Blocked(eye, point, target, blockers, observerRoot)) return true;
            }

            return false;
        }

        /// <summary>
        /// A mesma pergunta de <see cref="Blocked"/>, para quem precisa dela entre
        /// dois pontos quaisquer — por exemplo "deste poleiro chega-se ao jogador?".
        /// </summary>
        public static bool IsBlockedBetween(Vector3 from, Vector3 to, Transform ignoreRoot,
            LayerMask blockers) => Blocked(from, to, ignoreRoot, blockers);

        /// <summary>
        /// So a parte do enquadramento, sem os raios.
        ///
        /// Serve para a pergunta inversa — "posso pô-lo aqui sem ele nascer a
        /// frente do jogador?" — onde a oclusao **nao** deve valer: aparecer atras
        /// de uma parede que o jogador esta a olhar continua a ser aparecer no
        /// sitio errado no momento em que ele se mexe.
        /// </summary>
        public static bool IsInsideView(Camera camera, Vector3 point, float margin = 0f)
        {
            if (camera == null) return false;
            Vector3 viewport = camera.WorldToViewportPoint(point);
            return viewport.z > 0f &&
                   viewport.x >= margin && viewport.x <= 1f - margin &&
                   viewport.y >= margin && viewport.y <= 1f - margin;
        }
    }
}
