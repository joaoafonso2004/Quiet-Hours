using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pungent.Narrative
{
    /// <summary>
    /// Quem sabe re-atar as pontas depois de uma troca de cena.
    ///
    /// Um sistema que sobrevive a cena guarda referencias para objectos que nao
    /// sobreviveram. Em Unity um objecto destruido nao fica a null de verdade —
    /// fica um "null falso" que so se nota quando se compara. E por isso que o
    /// idioma que este projecto ja usa em todo o lado, `if (x == null) x =
    /// FindObjectOfType<X>()`, chega perfeitamente: depois da troca de cena as
    /// referencias velhas comparam como nulas e a busca volta a encontrar as novas.
    ///
    /// O unico problema era o sitio: esse codigo estava no `Awake`, e o `Awake` de
    /// um objecto persistente corre uma vez so, na primeira cena.
    /// </summary>
    public interface IRebindable
    {
        /// <summary>Volta a procurar o que ficou para tras. Tem de aguentar ser
        /// chamado muitas vezes e com tudo ja ligado.</summary>
        void Rebind();
    }

    /// <summary>
    /// A raiz dos sistemas que nao pertencem a nenhuma cena.
    ///
    /// O `ChapterDirector` guarda que acontecimentos ja passaram. Enquanto viveu no
    /// `PlayerRoot`, esse registo morria com a cena — sair do apartamento para a
    /// oficina dava um director novo, com a lista de acontecimentos vazia. O
    /// `day_four` tinha sido levantado no apartamento e o Dia 4 nunca chegava a
    /// comecar na garagem, sem erro nenhum a dizer porque. E a armadilha de sempre
    /// deste projecto, desta vez a atravessar cenas.
    ///
    /// Aqui os sistemas atravessam a troca de cena inteiros, e a seguir a cada
    /// carregamento pedem a quem sabe que volte a atar as pontas ao jogador novo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameSystemsRoot : MonoBehaviour
    {
        private static GameSystemsRoot instance;

        public static GameSystemsRoot Instance
        {
            get
            {
                // Recupera tambem de um domain reload no Editor: o objecto continua
                // na cena, mas o campo estatico volta a null.
                if (instance == null) instance = FindObjectOfType<GameSystemsRoot>();
                return instance;
            }
        }

        private void Awake()
        {
            // Cada cena traz o seu exemplar, para se poder abrir qualquer cena
            // sozinha no editor e ter jogo a funcionar. Em jogo a serio, o primeiro
            // e que tem o estado dos capitulos: e esse que fica.
            if (instance != null && !object.ReferenceEquals(instance, this))
            {
                // Desactivar antes de destruir: o `Destroy` so age no fim do frame e
                // sem isto os dois directores respondiam ao mesmo acontecimento.
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            instance = this;

            // `DontDestroyOnLoad` so aceita objectos de raiz.
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            // `OnEnable` volta a correr depois de recompilacoes durante Play. O
            // `Awake` nao: se o estatico tiver sido limpo, este e o ponto onde a
            // raiz persistente volta a assumir os sistemas que ja existem.
            if (instance == null) instance = this;
            if (!object.ReferenceEquals(instance, this)) return;

            SceneManager.sceneLoaded += OnSceneLoaded;
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            Rebind();
        }

        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnDestroy()
        {
            // ReferenceEquals e deliberado. A igualdade do Unity trata objectos
            // destruidos como null e podia deixar um duplicado limpar a instancia
            // sobrevivente no fim do frame.
            if (object.ReferenceEquals(instance, this)) instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!object.ReferenceEquals(instance, this)) return;
            Rebind();
        }

        /// <summary>
        /// Re-ata as pontas. Publico para as ferramentas de editor e para quem
        /// carregar cenas a mao.
        /// </summary>
        public void Rebind()
        {
            foreach (var rebindable in GetComponentsInChildren<IRebindable>(true))
                rebindable.Rebind();
        }
    }
}
