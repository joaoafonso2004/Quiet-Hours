using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pungent.EditorTools
{
    /// <summary>
    /// O que o apartamento tem de ter, e o que se parte quando nao tem.
    ///
    /// ---
    ///
    /// **Porque e que isto existe.**
    ///
    /// As ferramentas de ligacao procuram moveis pelo nome — `FindAnywhere("Kit_Sink")`
    /// — e quando nao os encontram escrevem um aviso e seguem em frente. E o
    /// comportamento certo: uma ferramenta que rebentasse por falta de um vaso deixava
    /// metade da passagem por montar.
    ///
    /// O preco disso e que **apagar um movel nao da erro nenhum**. Da um aviso numa
    /// consola que ja tem sessenta linhas, no meio de uma execucao que correu bem, e
    /// que ninguem le. O que se ve semanas depois e um susto que nao acontece, uma
    /// tarefa que nao aparece, ou uma luz que nunca se apaga — e a essa altura ja
    /// ninguem liga as duas coisas.
    ///
    /// Este ficheiro e o registo do que a casa precisa de ter, com **a consequencia de
    /// cada peca**. Corre-se, e diz o que falta e o que e que isso estraga.
    ///
    /// ---
    ///
    /// **Estado em 2026-08-10**, depois de terem sido apagados objectos a mao: os 60
    /// moveis do `ApartmentV2Dressing` estao todos presentes, e as 28 pecas que os
    /// outros sistemas procuram tambem. Nao ha nada em falta.
    ///
    /// A cena esta serializada em binario, por isso o historico do git nao diz que
    /// objectos desapareceram — e a razao de isto passar a existir em vez de uma lista
    /// escrita a mao daquilo que se perdeu de uma vez.
    ///
    /// **Nao cobre o que nao tem dono.** Decoracao solta, sujidade do `MESS_V2`,
    /// duplicados — nada disso esta aqui, porque nada disso partiria coisa nenhuma.
    /// Se apagaste um movel e ele nao aparece nesta lista, e porque nao faz falta a
    /// sistema nenhum.
    /// </summary>
    internal static class ApartmentInventory
    {
        /// <summary>Uma peca, e o que deixa de funcionar sem ela.</summary>
        private readonly struct Piece
        {
            public readonly string Name, Breaks;
            public Piece(string name, string breaks) { Name = name; Breaks = breaks; }
        }

        // ==================================================================
        // Pecas com consequencia nomeada. Cada uma destas foi verificada: o
        // sistema que a procura esta identificado, e a frase diz o que o
        // jogador deixa de ver.
        // ==================================================================
        private static readonly Piece[] Critical =
        {
            new Piece("Kit_Sink", "lavar a loica (Dia 1 e Dia 3), o copo de agua das 02:47, " +
                                  "e a luz da rua que se funde — mede-se a partir daqui"),
            new Piece("Kit_Table", "limpar a mesa no Dia 3, e a luz da casa de banho que " +
                                   "se acende com ele na cozinha"),
            new Piece("Kit_Chair_A", "a cadeira que roda atras dele na noite das 02:47"),
            new Piece("Kit_Worktop", "as duas refeicoes por fases (d1_eat, d3_eat)"),
            new Piece("Kit_Fridge", "a segunda metade das refeicoes por fases"),
            new Piece("Dresser_Tomas", "arrumar a roupa, e a gaveta entreaberta do Dia 3"),
            new Piece("Bed_Tomas", "dormir, e a altura a que a roupa por arrumar assenta"),
            new Piece("Bed_Rui", "o silencio de 3,6 s no quarto dele, no Dia 3"),
            new Piece("Living_Couch", "sentar-se no Dia 1"),
            new Piece("Living_Armchair", "a poltrona que roda sozinha na noite das 02:47"),
            new Piece("Balcony_Stool", "o banco puxado da guarda, no Dia 3"),
            new Piece("Chair_Desk_Tomas", "a cadeira da secretaria rodada, depois de ele " +
                                          "entrar no quarto do Rui"),
            new Piece("WashingMachine", "por uma maquina no Dia 3, e a porta da varanda " +
                                        "que se abre enquanto ele esta la"),
            new Piece("Laptop_Screen", "recomecar o upload as 02:47"),
            new Piece("Router", "o mini-jogo dos cabos e o silencio total do corredor"),
            new Piece("ElectricalPanel", "o quadro que se abre no Dia 3 e o plano de camara " +
                                         "a saida de casa"),
            new Piece("KeyShelf_Hall", "as chaves do prologo e a prateleira vazia do Dia 5"),
            new Piece("Task_Smoke", "fumar na varanda — conta para as tarefas do Dia 1"),
            new Piece("Window_North_1", "saber se ele esta a olhar pela janela da cozinha"),
            new Piece("Door_Bedroom_Tomas", "trancar o quarto, a fechadura que emperra, " +
                                            "e a conversa a porta do Dia 3"),
            new Piece("Door_Bedroom_Rui", "a porta que a camara agarra as 02:47 e no Dia 5"),
            new Piece("Door_Balcony", "a porta que se abre sozinha no Dia 3"),
            new Piece("Door_Front_3B", "sair de casa, e a fuga do Dia 5"),
            new Piece("NPC_Rui", "tudo o que envolve o Rui"),
            new Piece("GAME_SYSTEMS", "capitulos, telemovel, cartoes, corte a preto e epilogo"),
            new Piece("APARTMENT_SYSTEMS", "prologo, Dia 2, Dia 3 e a conversa a porta"),
        };

        // Luzes com nome. Apagar uma nao da erro — da uma divisao que nunca acende,
        // ou um momento de atmosfera que nao acontece.
        private static readonly Piece[] Lights =
        {
            new Piece("Bathroom_Light", "a luz que se acende sozinha no Dia 3"),
            new Piece("Bedroom_Rui_Light", "a risca de luz por baixo da porta dele, as 02:47"),
            new Piece("Kitchen_Light", "a cozinha, e a casa vazia do Dia 3"),
            new Piece("Living_Light", "a sala, e a casa vazia do Dia 3"),
            new Piece("Corridor_Light", "o corredor"),
            new Piece("Corridor_Light_West", "o corredor a poente"),
            new Piece("Bedroom_Light", "o quarto do Tomas"),
            new Piece("Hall_Light", "a entrada"),
            new Piece("Dining_Light", "a sala de jantar"),
            new Piece("StreetGlow_N_-8", "a lampada da rua que se funde as 02:47"),
        };

        /// <summary>
        /// Os 60 moveis que o `ApartmentV2Dressing` coloca. Sem consequencia escrita um
        /// a um de propósito: o que se perde e a divisao a parecer vazia, e isso ve-se.
        /// </summary>
        private static readonly string[] Dressing =
        {
            "Balcony_Plant_A", "Balcony_Plant_B", "Balcony_Stool", "Basket_Laundry", "Bathtub",
            "Bed_Rui", "Bed_Tomas", "Bin_Storage", "Bookshelf_Rui", "Bookshelf_Tomas",
            "Carpet_Rui", "Carpet_Tomas", "Chair_Desk_Tomas", "Dining_Carpet", "Dining_Chair_E",
            "Dining_Chair_N", "Dining_Chair_S", "Dining_Chair_W", "Dining_Shelf", "Dining_Table",
            "Drawer_Storage", "Dresser_Tomas", "KeyShelf_Hall", "Kit_Cabinet_A", "Kit_Cabinet_B",
            "Kit_Chair_A", "Kit_Chair_B", "Kit_Drawers", "Kit_Fridge", "Kit_Oven", "Kit_Plant",
            "Kit_Sink", "Kit_Table", "Kit_Trash", "Living_Armchair", "Living_Bookshelf",
            "Living_Carpet", "Living_CoffeeTable", "Living_Couch", "Living_Curtains",
            "Living_FloorLamp", "Living_MediaUnit", "Living_Plant", "Mirror_Bath",
            "NightStand_Rui", "NightStand_Tomas", "Plant_Hall", "Plant_Tomas", "Shelf_Laundry",
            "Shelf_Storage", "ShoeCabinet_Hall", "Sink_Bath", "Supplies_Laundry", "Toilet",
            "Towel_Bath", "Trash_Hall", "Trash_Rui", "Trash_Tomas", "Wardrobe_Rui",
            "WashingMachine",
        };

        [MenuItem("Pungent/Debug/Inventario do apartamento", false, 202)]
        internal static void Check()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Contains("Apartment"))
            {
                Debug.LogError("[Inventario] Abrir a `Apartment_Blockout_V2` primeiro.");
                return;
            }

            var present = new HashSet<string>();
            foreach (var t in Object.FindObjectsOfType<Transform>(true)) present.Add(t.name);

            var report = new System.Text.StringBuilder();
            int missing = 0;

            missing += Section(report, present, "PECAS COM CONSEQUENCIA", Critical);
            missing += Section(report, present, "LUZES", Lights);

            var lostDressing = new List<string>();
            foreach (string name in Dressing)
                if (!present.Contains(name)) lostDressing.Add(name);

            if (lostDressing.Count > 0)
            {
                report.Append("\nMOVEIS DO DRESSING em falta (").Append(lostDressing.Count)
                      .Append(" de ").Append(Dressing.Length).Append("):\n  ")
                      .Append(string.Join(", ", lostDressing))
                      .Append("\n  -> `Repair Missing Furniture Meshes` devolve a malha a quem a "
                            + "perdeu; um movel apagado por inteiro so volta com `Dress Apartment V2`, "
                            + "que reconstroi tudo e obriga a correr outra vez as ligacoes por cima.\n");
                missing += lostDressing.Count;
            }

            // **Sem malha conta como em falta.** Um movel invisivel esta na cena para
            // as ferramentas e nao esta para o jogador — foi assim que a poltrona da
            // sala passou meses a rodar sozinha sem ninguem a ver rodar.
            var invisible = new List<string>();
            foreach (string name in Dressing)
            {
                GameObject go = Find(name);
                if (go == null) continue;
                if (go.GetComponentInChildren<Renderer>(true) == null) invisible.Add(name);
            }

            if (invisible.Count > 0)
                report.Append("\nSEM MALHA (existem, nao se veem): ")
                      .Append(string.Join(", ", invisible))
                      .Append("\n  -> correr `Repair Missing Furniture Meshes`.\n");

            if (missing == 0 && invisible.Count == 0)
            {
                Debug.Log("[Inventario] A casa esta completa: " +
                          (Critical.Length + Lights.Length + Dressing.Length) +
                          " pecas verificadas, nenhuma em falta, nenhuma invisivel.");
                return;
            }

            Debug.LogWarning("[Inventario] " + missing + " peca(s) em falta, " +
                             invisible.Count + " sem malha.\n" + report);
        }

        private static int Section(System.Text.StringBuilder report, HashSet<string> present,
            string title, Piece[] pieces)
        {
            var lost = new List<Piece>();
            foreach (var piece in pieces)
                if (!present.Contains(piece.Name)) lost.Add(piece);

            if (lost.Count == 0) return 0;

            report.Append('\n').Append(title).Append(":\n");
            foreach (var piece in lost)
                report.Append("  ").Append(piece.Name).Append(" -> perde-se ")
                      .Append(piece.Breaks).Append('\n');

            return lost.Count;
        }

        private static GameObject Find(string name)
        {
            foreach (var t in Object.FindObjectsOfType<Transform>(true))
                if (t.name == name) return t.gameObject;
            return null;
        }
    }
}
