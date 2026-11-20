using Colossal;
using Microsoft.Win32;
using System.Collections.Generic;

namespace BitulaMod {
    public class LocaleShoppingEN : IDictionarySource {
        private readonly Setting m_Setting;

        public LocaleShoppingEN(Setting setting) {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts) {

            indexCounts["BitulaMod.LIFEPATH_StartShopResource"] = 1;
            indexCounts["BitulaMod.LIFEPATH_GoToShop"] = 1;
            indexCounts["BitulaMod.LIFEPATH_ShopOnline"] = 1;
            indexCounts["BitulaMod.LIFEPATH_ShopNoResource"] = 1;
            indexCounts["BitulaMod.LIFEPATH_ShopNoMoney"] = 1;
            indexCounts["BitulaMod.LIFEPATH_ShopNoPath"] = 1;
            indexCounts["BitulaMod.LIFEPATH_ShopDoesNotExist"] = 1;
            indexCounts["BitulaMod.LIFEPATH_ShopDestinationUnavailable"] = 1;
            indexCounts["BitulaMod.LIFEPATH_ResourceBought"] = 1;
            indexCounts["BitulaMod.LIFEPATH_NoShoppingNeed"] = 3;
            indexCounts["BitulaMod.LIFEPATH_ShoppingTryEnd"] = 3;

            return new Dictionary<string, string> {
                { m_Setting.GetOptionTabLocaleID("Shopping"), "Shopping" },
                { m_Setting.GetOptionGroupLocaleID("Shopping"), "Shopping" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.NormalShopping)), "Normal Shopping" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.NormalShopping)), "Enables more realistic individual shopping behavior for citizens. Vanilla shopping behavior is gradually restored as the city grows, according to the configured milestone increment." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ShoppingIntervalMin)), "Minimum shopping duration" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ShoppingIntervalMin)), "Sets the minimum percentage of the available shopping time that citizens spend shopping." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ShoppingIntervalMax)), "Maximum shopping duration" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ShoppingIntervalMax)), "Sets the maximum percentage of the available shopping time that citizens spend shopping." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.PhysicalVirtualShopping)), "Physical shopping" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.PhysicalVirtualShopping)), "Allows citizens using Small City shopping behavior to physically visit stores when purchasing virtual goods instead of automatically buying them remotely." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ShoppingMaxFailedAttempts)), "Shopping attempts" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ShoppingMaxFailedAttempts)), "Maximum unsuccessful shopping attempts per day. The counter resets after a successful purchase or at the beginning of a new day." },

                { "BitulaMod.LIFEPATH_StartShopResource:0", "I need to buy something. {LINK_NAME_1}" },
                { "BitulaMod.LIFEPATH_LINK_StartShopResource", "I need to buy {0}." },
                { "BitulaMod.LIFEPATH_GoToShop:0", "I found a store that has what I need. Let's go to {LINK_1}." },
                { "BitulaMod.LIFEPATH_ShopOnline:0", "I ordered it online instead." },
                { "BitulaMod.LIFEPATH_ShopNoResource:0", "The store didn't have enough in stock." },
                { "BitulaMod.LIFEPATH_ShopNoMoney:0", "I couldn't afford it." },
                { "BitulaMod.LIFEPATH_ShopNoPath:0", "I couldn't find a store I could reach in time." },
                { "BitulaMod.LIFEPATH_ShopDoesNotExist:0", "I couldn't find a store selling what I wanted." },
                { "BitulaMod.LIFEPATH_ShopDestinationUnavailable:0", "I found a store, but it's no longer available." },
                { "BitulaMod.LIFEPATH_ResourceBought:0", "I bought something. {LINK_NAME_1}" },
                { "BitulaMod.LIFEPATH_LINK_ResourceBought", "I bought {0} units of {1} for ₡{2}." },
                { "BitulaMod.LIFEPATH_NoShoppingNeed:0", "I checked the shopping list. Nothing on it. That's a first!" },
                { "BitulaMod.LIFEPATH_NoShoppingNeed:1", "My wallet can relax today. We actually have everything we need." },
                { "BitulaMod.LIFEPATH_NoShoppingNeed:2", "The shops will have to survive without me today. Our cupboards are well stocked." },
                { "BitulaMod.LIFEPATH_ShoppingTryEnd:0", "I've searched enough shops for one day. Maybe tomorrow I'll have better luck." },
                { "BitulaMod.LIFEPATH_ShoppingTryEnd:1", "My shopping list is still full, but my patience has officially run out." },
                { "BitulaMod.LIFEPATH_ShoppingTryEnd:2", "If finding what I need is this much work, I think I'll just go home." },
            };
        }
        public void Unload() {
        }
    }
}