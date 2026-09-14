using Pungent.Interaction;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// Onde o jogador se escondeu, e por quanto tempo isso importa.
    ///
    /// ---
    ///
    /// **Uma coisa so, e um nome so.** Isto guarda o nome do esconderijo em que o
    /// Tomas se meteu na manha do Dia 3, quando o Rui entrou em casa e nao
    /// aconteceu nada (ver <see cref="NothingHappened"/>). Nada mais. Nao ha
    /// contagem, nao ha tempo, nao ha historico — um numero a subir seria mais uma
    /// variavel do §6.2 que ninguem le, e ja ha cinco dessas.
    ///
    /// Quem o le e a noite do Dia 5: o <see cref="NPC.RuiHunt"/> comeca a procurar
    /// pelo sitio onde tu ja te escondeste uma vez.
    ///
    /// ---
    ///
    /// **Porque e que vive no `GAME_SYSTEMS` e nao no beat que o escreve.**
    ///
    /// Entre a manha do Dia 3 e a noite do Dia 5 o apartamento e **recarregado** —
    /// e a mesma cena, mas e outra vez, e nada dela sobrevive. Um campo dentro do
    /// `NothingHappened` morria com o objecto que o escreveu, e o Dia 5 encontrava
    /// um componente novo com a memoria em branco. Sem erro nenhum: a caca corria
    /// exactamente como corre hoje e ninguem dava por nada.
    ///
    /// Por isso e um <see cref="IRebindable"/> na raiz persistente, ao lado do
    /// registo de acontecimentos e do blackboard, que estao la pela mesma razao.
    ///
    /// **Por nome e nao por referencia.** Uma referencia para o `HidingSpot` do
    /// Dia 3 e um ponteiro para um objecto de uma cena que ja nao existe. O nome
    /// atravessa a recarga, e do outro lado ha um objecto com o mesmo nome porque
    /// os quatro esconderijos sao construidos pela mesma ferramenta.
    ///
    /// ---
    ///
    /// **Na duvida, cala-se.** Se o jogador nao se escondeu, se o nome nao existir
    /// nesta cena, ou se a manha nunca aconteceu, isto responde que nao sabe — e
    /// quem lhe pergunta fica exactamente com o comportamento que tinha antes de
    /// isto existir. E a unica maneira de nao poder partir o Dia 5, que ja esteve
    /// inerte duas vezes sem dar erro.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HidingMemory : MonoBehaviour, IRebindable
    {
        [Tooltip("O nome do esconderijo. Serializado para se poder ver no inspector "
               + "e para se poder forcar a mao ao testar — em jogo quem o escreve e "
               + "o `NothingHappened`.")]
        [SerializeField] private string spotName = string.Empty;

        /// <summary>O nome do sitio, ou vazio.</summary>
        public string SpotName => spotName;

        /// <summary>Houve um sitio.</summary>
        public bool Remembers => !string.IsNullOrWhiteSpace(spotName);

        /// <summary>
        /// O exemplar vivo, resolvido na leitura e nunca guardado no `Awake`.
        ///
        /// A mesma nota do `ChapterDirector.Resolve`, e pela mesma cicatriz: na
        /// recarga do apartamento para o climax ha **dois** `GAME_SYSTEMS` vivos no
        /// mesmo frame — o que veio da cena anterior e o que a cena nova traz e que
        /// se desactiva a seguir. Guardar o primeiro que aparecesse dava, metade das
        /// vezes, o exemplar com a memoria vazia.
        /// </summary>
        public static HidingMemory Resolve(HidingMemory candidate = null)
        {
            if (candidate != null) return candidate;

            var root = GameSystemsRoot.Instance;
            if (root != null)
            {
                var persistent = root.GetComponentInChildren<HidingMemory>(true);
                if (persistent != null) return persistent;
            }

            return FindObjectOfType<HidingMemory>(true);
        }

        /// <summary>
        /// Guarda o sitio. Chamado uma vez, no fim da manha do Dia 3.
        ///
        /// Fica com o **ultimo** e nao com o primeiro, que e a mesma regra do
        /// `NothingHappened`: quem saiu de um esconderijo e se enfiou noutro tomou a
        /// segunda decisao com mais informacao.
        ///
        /// Um nome vazio nao apaga o que la esta. Apagar era dar ao acaso de uma
        /// chamada mal ordenada o poder de desfazer a unica coisa que este
        /// componente sabe.
        /// </summary>
        public void Remember(string spot)
        {
            if (string.IsNullOrWhiteSpace(spot)) return;
            spotName = spot;
        }

        /// <summary>Esquece. Existe para os testes e para um jogo novo.</summary>
        public void Forget() => spotName = string.Empty;

        /// <summary>
        /// O esconderijo lembrado, se existir **nesta** cena.
        ///
        /// Procurado incluindo inactivos de proposito: no apartamento os quatro
        /// vivem dentro do `CLIMAX_CONTENT`, que so acende quando o
        /// <see cref="ClimaxStage"/> mandar, e quem pergunta por isto pode
        /// perguntar antes disso.
        ///
        /// Devolve nulo em vez de adivinhar. Uma cena sem esconderijo com aquele
        /// nome — a garagem, a estrada, ou um apartamento a que alguem tenha
        /// mudado os nomes — nao e um erro: e uma cena onde esta memoria nao tem
        /// nada a dizer.
        /// </summary>
        public HidingSpot ResolveSpot()
        {
            if (!Remembers) return null;

            foreach (var spot in FindObjectsOfType<HidingSpot>(true))
                if (spot != null && spot.gameObject.name == spotName) return spot;

            return null;
        }

        /// <summary>
        /// Nao ha pontas para re-atar — e e por isso que isto e um `IRebindable`.
        ///
        /// O contrato existe para os sistemas persistentes voltarem a encontrar o
        /// jogador novo depois de uma troca de cena. Este nao aponta para nada da
        /// cena, e essa e exactamente a propriedade que o faz funcionar: nao ha
        /// referencia nenhuma que a recarga possa partir. Implementa o contrato
        /// para viver na mesma lista dos outros e para quem ler a raiz nao ter de
        /// perguntar porque e que este ficou de fora.
        /// </summary>
        public void Rebind() { }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(string startingSpot) => spotName = startingSpot;
#endif
    }
}
