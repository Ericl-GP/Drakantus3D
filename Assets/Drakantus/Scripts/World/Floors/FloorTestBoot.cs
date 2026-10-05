using System.Collections;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// "Testar andar (Play)" do Criador de Andares: o editor grava PlayerPrefs (andar, nível, classe) e
    /// entra no Play na cena Drakantus. Aqui, depois que Game e HUD existem, criamos um herói de teste
    /// no nível pedido, pulamos a tela de título e viajamos direto para o andar.
    /// (O editor faz cópia do save real antes e devolve ao sair do Play.)
    /// </summary>
    public class FloorTestBoot : MonoBehaviour
    {
        public const string KeyFloor = "Drakantus.TestFloor";
        public const string KeyLevel = "Drakantus.TestLevel";
        public const string KeyClass = "Drakantus.TestClass";

        string floorId, classId;
        int level;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            string id = PlayerPrefs.GetString(KeyFloor, "");
            if (string.IsNullOrEmpty(id)) return;
            var go = new GameObject("FloorTestBoot");
            DontDestroyOnLoad(go);
            var b = go.AddComponent<FloorTestBoot>();
            b.floorId = id;
            b.level = Mathf.Clamp(PlayerPrefs.GetInt(KeyLevel, 1), 1, 99);
            b.classId = PlayerPrefs.GetString(KeyClass, "");
            PlayerPrefs.DeleteKey(KeyFloor);
            PlayerPrefs.Save();
        }

        IEnumerator Start()
        {
            // espera Game + HUD + tela de título
            float t = 0f;
            while ((Game.I == null || HUD.I == null || HUD.I.ModalKind != "title") && t < 10f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (Game.I == null || HUD.I == null)
            {
                Debug.LogWarning("[Drakantus] Teste de andar: o Game não iniciou (abra a cena Drakantus).");
                Destroy(gameObject);
                yield break;
            }
            yield return null;

            GameData.Load();
            GameState.NewGame("Testador");
            string cls = GameData.Classes.ContainsKey(classId ?? "") ? classId : (GameData.ClassOrder.Count > 0 ? GameData.ClassOrder[0].id : "");
            GameState.P.classId = cls;
            GameState.P.level = level;
            GameState.P.xp = 0;
            GameState.P.registered = true;
            GameState.P.coins = 500;
            GameState.Grant("health_potion", 5);
            GameState.Grant("mana_potion", 3);
            GameState.Recalc();
            GameState.P.hp = GameState.maxHp;
            GameState.P.mp = GameState.maxMp;

            // fecha a tela de título do mesmo jeito que o botão "Novo jogo" (método privado do HUD)
            var m = typeof(HUD).GetMethod("StartFromTitle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (m != null) m.Invoke(HUD.I, null);
            else
            {
                // plano B: troca a tela de título por uma janela fechável e fecha
                HUD.I.ClassWindow(false);
                HUD.I.CloseModal();
                Game.I.StartGame();
            }

            // espera chegar na cidade e viaja para o andar
            t = 0f;
            while ((Game.I == null || Game.I.Traveling || Game.I.mapId != "town") && t < 15f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (Game.I != null)
            {
                if (HUD.I != null && HUD.I.HasModal) HUD.I.CloseModal();
                Game.I.Travel(floorId, "entrance");
                if (HUD.I != null) HUD.I.Toast("Teste do andar " + floorId + " · herói nível " + level);
            }
            Destroy(gameObject, 2f);
        }
    }
}
