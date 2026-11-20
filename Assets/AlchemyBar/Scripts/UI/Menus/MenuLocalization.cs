using System.Collections.Generic;

namespace AlchemyBar.UI.Menus
{
    public static class MenuLocalization
    {
        public const int English = 0;
        public const int Russian = 1;

        private static readonly Dictionary<string, string> Ru = new Dictionary<string, string>
        {
            { "A WITCH'S POTION CAFÉ", "КАФЕ ВЕДЬМИНЫХ ЗЕЛИЙ" },
            { "brew · enchant · serve", "вари · колдуй · подавай" },
            { "Start", "Начать" },
            { "Settings", "Настройки" },
            { "Exit", "Выйти" },

            { "Music Volume", "Громкость музыки" },
            { "Spell Effects", "Звуки заклинаний" },
            { "Ambient Whispers", "Фоновые звуки" },
            { "Brew Shake FX", "Вибрация при варке" },
            { "Tongue of the Realm", "Язык" },
            { "Return", "Назад" },
            { "Seal the Runes", "Сохранить" },

            { "ENCHANTMENT PAUSED", "ЧАРЫ ПРИОСТАНОВЛЕНЫ" },
            { "The Cauldron Rests", "Котёл отдыхает" },
            { "\"Even the greatest witches must pause to stir their thoughts…\"", "«Даже великим ведьмам нужно остановиться и собраться с мыслями…»" },
            { "Continue the Brew", "Продолжить варку" },
            { "Mystical Settings", "Настройки" },
            { "Save & Return", "Сохранить и выйти" },
            { "Abandon the Ritual", "Прервать ритуал" },
            { "POTIONS BREWED", "ЗЕЛИЙ" },
            { "GOLD EARNED", "ЗОЛОТО" },
            { "SESSION TIME", "ВРЕМЯ" },
        };

        public static bool Has(string key) => Ru.ContainsKey(key);

        public static string Get(string key, int language)
        {
            if (language == Russian && Ru.TryGetValue(key, out string ru)) return ru;
            return key;
        }
    }
}
