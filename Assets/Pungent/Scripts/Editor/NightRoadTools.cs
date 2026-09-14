using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Dialogue;
using Pungent.Driving;

namespace Pungent.EditorTools
{
    /// <summary>
    /// As duas coisas que a estrada precisa e que o `NightRoadBuilder` nao dava: as
    /// vozes do encontro, e uma maneira de la chegar sem conduzir quatro minutos.
    ///
    /// Vivem aqui e nao dentro do builder por uma razao pratica: o builder **apaga a
    /// raiz e volta a montar tudo**, e ninguem quer reconstruir uma estrada inteira
    /// para afinar o pitch de uma voz. Isto acrescenta ao que ja esta na cena, e
    /// pode correr as vezes que forem precisas.
    /// </summary>
    internal static class NightRoadTools
    {
        private const string VoiceHim = "Stranger_Voice";
        private const string VoiceMe = "Tomas_Voice";

        // ------------------------------------------------------------------
        // As vozes

        /// <summary>
        /// Da voz aos dois que falam na berma.
        ///
        /// **Ate agora nao falava ninguem.** As sete falas do estranho passavam
        /// `null` como voz e sairam sempre em silencio absoluto — texto branco a
        /// aparecer numa estrada preta, sem uma sílaba. Na casa, o Rui tem voz desde
        /// o prologo; a unica pessoa do jogo que aparece do escuro a saber coisas a
        /// teu respeito era muda.
        ///
        /// **A do estranho e do mundo, a do Tomas nao.** A dele sai do corpo dele, a
        /// doze metros e em contraluz: durante metade da cena o jogador nao lhe ve a
        /// cara, e de que lado vem a voz e a unica coisa que diz onde ele esta. A do
        /// Tomas nao vem de sitio nenhum — vem de dentro da cabeca de quem joga.
        /// Espacial, o volume da propria voz dele mudava conforme para onde o jogador
        /// estivesse virado.
        ///
        /// **As janelas de pitch nao se sobrepoem, e isso nao e decoracao.** Sem
        /// gravacoes, as sílabas sao geradas por codigo e sao as mesmas para toda a
        /// gente: duas vozes com a mesma janela sao literalmente a mesma pessoa a
        /// falar sozinha, e esta e a unica cena do jogo onde ha duas a falar.
        /// </summary>
        [MenuItem("Pungent/Blockout/Wire Road Voices", false, 41)]
        internal static void WireVoices()
        {
            if (BuildGuard.Blocked("WireRoadVoices")) return;

            var stranger = Object.FindObjectOfType<StrangerEncounter>(true);
            if (stranger == null)
            {
                Debug.LogError("[Estrada] Sem `StrangerEncounter` nesta cena. " +
                               "Abrir a `Night_Road`, ou correr 'Build Night Road'.");
                return;
            }

            // Filhas do proprio componente, e nao soltas na raiz: uma ferramenta que
            // cria objectos fora da sua propria raiz nao e idempotente, e esta ja
            // aprendeu isso com as tarefas domesticas.
            var him = Voice(stranger.transform, VoiceHim,
                localPosition: new Vector3(0f, 1.62f, 0f),   // a altura da boca dele
                spatial: true,
                pitch: new Vector2(0.86f, 0.95f),            // mais grave: e um homem mais velho
                loudness: 0.34f, minDistance: 1.4f, maxDistance: 22f);

            // A posicao desta e irrelevante — nao e espacial — mas fica junta a outra
            // para nao andar ninguem a procura dela na hierarquia.
            var me = Voice(stranger.transform, VoiceMe,
                localPosition: new Vector3(0f, 1.62f, 0f),
                spatial: false,
                pitch: new Vector2(1.02f, 1.12f),            // mais fino, e mais baixo de volume
                loudness: 0.22f, minDistance: 1f, maxDistance: 5f);

            stranger.EditorSetVoices(him, me);
            EditorUtility.SetDirty(stranger);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Estrada] Vozes ligadas: o estranho sai do corpo dele, o Tomas " +
                      "sai de dentro da cabeca de quem joga.");
        }

        private static NpcMumbleVoice Voice(Transform parent, string name,
            Vector3 localPosition, bool spatial, Vector2 pitch, float loudness,
            float minDistance, float maxDistance)
        {
            var found = parent.Find(name);
            GameObject go;
            if (found != null) go = found.gameObject;
            else
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(go, "Wire road voices");
            }

            go.transform.localPosition = localPosition;

            // O `NpcMumbleVoice` pede `AudioSource`, e o `RequireComponent` so o
            // acrescenta ao adicionar pelo inspector.
            if (go.GetComponent<AudioSource>() == null) go.AddComponent<AudioSource>();

            var voice = go.GetComponent<NpcMumbleVoice>();
            if (voice == null) voice = go.AddComponent<NpcMumbleVoice>();

            voice.EditorConfigure(spatial, pitch, loudness, minDistance, maxDistance);
            EditorUtility.SetDirty(voice);
            return voice;
        }

        // ------------------------------------------------------------------
        // O atalho

        /// <summary>
        /// Poe a cena no instante em que o estranho chega ao carro, em Play.
        ///
        /// A escolha deste capitulo esta no fim de quatro minutos de alcatrao, e
        /// experimentar as duas respostas custava oito minutos de conducao por par de
        /// tentativas. Uma decisao que so se pode sentir de meia em meia hora nao
        /// chega a ser afinada — e afinar esta e o unico trabalho que falta fazer-lhe.
        ///
        /// O trabalho todo esta no <see cref="NightRoadDirector.EditorSkipToEncounter"/>,
        /// ao lado do codigo que ele imita. Um atalho escrito longe do capitulo que
        /// salta e um atalho que deixa de bater certo na primeira vez que o capitulo
        /// mudar, sem ninguem dar por isso.
        /// </summary>
        [MenuItem("Pungent/Debug/Estrada: saltar para o encontro", false, 200)]
        internal static void SkipToEncounter()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Estrada] Entrar em Play na `Night_Road` primeiro. " +
                                 "Isto poe a cena a meio; nao a monta.");
                return;
            }

            var director = Object.FindObjectOfType<NightRoadDirector>(true);
            if (director == null)
            {
                Debug.LogWarning("[Estrada] Sem `NightRoadDirector`: esta e a cena certa?");
                return;
            }

            director.EditorSkipToEncounter();
        }

        [MenuItem("Pungent/Debug/Estrada: saltar para o encontro", true)]
        private static bool SkipToEncounterEnabled() => Application.isPlaying;
    }
}
