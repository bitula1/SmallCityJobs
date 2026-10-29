import { selectedInfo } from "cs2/bindings";
import { getModule } from "cs2/modding";
import { useEffect, useState, useRef } from "react";
import { createPortal } from "react-dom";
import { bindValue, useValue } from "cs2/api";
import { setShoppersTabSelected, subscribeShoppersTab, isShoppersTabSelected, selectCustomTab } from "mods/common/state";
import shoppersIcon from "./icons/ShoppersTab.svg";
import shoppingIcon from "./icons/shopping.svg";
import goingToShopIcon from "./icons/going-to-shop.svg";
import idlingIcon from "./icons/shopper-idling.svg";
import goingToAnotherShopIcon from "./icons/another-shopping.svg";
import goingToLeisureIcon from "./icons/shopper-going-to-leisure.svg";
import goingToWorkIcon from "./icons/shopping-going-to-work.svg";
import goingHomeIcon from "./icons/shopper-going-home.svg";
import notAnotherShoppingIcon from "./icons/not-another-shopping.svg";
import notLeisureIcon from "./icons/not-leisure.svg";
import { Age, Wealth, ageIcons, wealthIcons, Family, familyIcons } from "../common/state";
import "./ShoppersTab.css";

enum ShopperStatus {
    None = 0,
    Shopping = 1,
    GoingToShop = 2,
    Idling = 3,
    GoingToHome = 4,
    GoingToLeisure = 5,
    GoingToWork = 6,
    GoingToAnotherShop = 7,
    NotLeisure = 8,
    NotAnotherShopping = 9
}

type Entity = {
    index: number;
    version: number;
};


type Shopper = {
    entity: Entity;
    name: any;
    status: ShopperStatus;
    wealth: Wealth;
    age: Age;
    family: Family;
    shopperHours: string;
    resource: number;
    money: number;
};

const shoppers$ = bindValue<Shopper[]>(
    "BitulaMod",
    "shoppers",
    []
);

const panelStyles: any = getModule(
    "game-ui/game/components/selected-info-panel/selected-info-panel.module.scss",
    "classes"
);

const Tab: any = getModule(
    "game-ui/common/tabs/tabs.tsx",
    "Tab"
);

const TintedIcon: any = getModule(
    "game-ui/common/image/tinted-icon.tsx",
    "TintedIcon"
);

const Avatar: any = getModule(
    "game-ui/game/components/avatars/avatars.tsx",
    "Avatar"
);

const householdStyles: any = getModule(
    "game-ui/game/components/selected-info-panel/selected-info-sections/shared-sections/household-sidebar-section/household-sidebar-section.module.scss",
    "classes"
);

const useLocalizedName: any = getModule(
    "game-ui/common/localization/localized-entity-name.tsx",
    "useLocalizedName"
);

const LocalizedNumber: any = getModule(
    "game-ui/common/localization/localized-number.tsx",
    "LocalizedNumber"
);

const Tooltip: any = getModule(
    "game-ui/common/tooltip/tooltip.tsx",
    "Tooltip"
);

const statusIcons: Partial<Record<ShopperStatus, string>> = {
    [ShopperStatus.Shopping]: shoppingIcon,
    [ShopperStatus.GoingToShop]: goingToShopIcon,
    [ShopperStatus.Idling]: idlingIcon,
    [ShopperStatus.GoingToHome]: goingHomeIcon,
    [ShopperStatus.GoingToLeisure]: goingToLeisureIcon,
    [ShopperStatus.GoingToWork]: goingToWorkIcon,
    [ShopperStatus.GoingToAnotherShop]: goingToAnotherShopIcon,
    [ShopperStatus.NotLeisure]: notLeisureIcon,
    [ShopperStatus.NotAnotherShopping]: notAnotherShoppingIcon,
};

export const ACTIONS_SECTION = selectedInfo.SectionType.Actions;

const ShopperRow = ({ shopper }: { shopper: Shopper }) => {
    const name = useLocalizedName(shopper.name);
    const shopperHoursRef = useRef<string>("");

    if (shopper.shopperHours) {
        shopperHoursRef.current = shopper.shopperHours;
    }

    const shopperHours = shopper.shopperHours || shopperHoursRef.current;
    

    return (
            <div
                className={`${householdStyles.item} customer-row`}
                style={{
                    backgroundColor: shopper.resource === 0 ? "red" : undefined
                }}
                onClick={() => selectedInfo.selectEntity(shopper.entity)}
            >
            <Avatar
                entity={shopper.entity}
                className={householdStyles.avatar}
            />

            <div className={householdStyles.itemLabel}>
                {name}
            </div>   

            <img
                src={statusIcons[shopper.status]}
                className="shopper-status-icon"
            />

            <img
                src={ageIcons[shopper.age]}
                className="shopper-age-icon"
            />

            <img
                src={familyIcons[shopper.family]}
                className="shopper-family-icon"
            />

            {shopperHours && (
                <span className="shopper-leisure-hours">
                    [{shopperHours}]
                </span>
            )}

            <Tooltip
                tooltip={
                    <LocalizedNumber
                        value={shopper.money}
                        unit="money"
                    />
                }
            >
                <img
                    src={wealthIcons[shopper.wealth]}
                    className="shopper-wealth-icon"
                />
            </Tooltip>
        </div>
    );
};

export const ShoppersPanel = () => {
    const shoppers = useValue(shoppers$);

    return (
        <>
            {shoppers.map((shopper) => (
                <ShopperRow
                    key={`${shopper.entity.index}:${shopper.entity.version}`}
                    shopper={shopper}
                />
            ))}
        </>
    );
};

export const ShoppersTab = (componentList: any): any => {
    

    const VanillaActionsSection =
        componentList[ACTIONS_SECTION];

    if (!VanillaActionsSection) {
        console.log("BitulaMod: ActionsSection not found");
        return componentList;
    }

    componentList[ACTIONS_SECTION] = (props: any) => {

        const selectedUITag = useValue(selectedInfo.selectedUITag$);
        const shoppers = useValue(shoppers$);
        const showShoppersTab = shoppers.length > 0;
        const [tabBar, setTabBar] = useState<Element | null>(null);
        const [shoppersSelected, setShoppersSelected] = useState(isShoppersTabSelected());

        useEffect(() => {
            const element = document.getElementsByClassName(panelStyles.tabBar)[0];
            if (element) {
                setTabBar(element);
            }
        }, []);

        useEffect(() => {
            return subscribeShoppersTab(setShoppersSelected);
        }, []);

        useEffect(() => {
            if (!showShoppersTab) {
                setShoppersTabSelected(false);
            }
        }, [showShoppersTab]);

        useEffect(() => {
            console.log("BitulaMod: selectedUITag =", selectedUITag);
        }, [selectedUITag]);       
        

        return (
            <>
                <VanillaActionsSection {...props} />

                {showShoppersTab && tabBar && createPortal(
                    <Tab
                        id={4}
                        selectedId={shoppersSelected ? 4 : 0}
                        onSelect={() => {
                            selectCustomTab("shoppers");
                        }}
                    >
                        <TintedIcon
                            src={shoppersIcon}
                            className={panelStyles.tabIcon}
                        />
                    </Tab>,
                    tabBar
                )}                
            </>
        );
    };


    return componentList;
};