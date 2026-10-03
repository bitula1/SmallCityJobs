import { selectedInfo } from "cs2/bindings";
import { getModule } from "cs2/modding";
import { useEffect, useState, useRef } from "react";
import { createPortal } from "react-dom";
import { bindValue, useValue } from "cs2/api";
import { setCustomersTabSelected, subscribeCustomersTab, isCustomersTabSelected, selectCustomTab } from "mods/common/state";
import customersIcon from "./icons/customers-group.svg";
import leisuringIcon from "./icons/leisuring.svg";
import goingHomeIcon from "./icons/going-home.svg";
import goingToAnotherLeisureIcon from "./icons/going-to-another-leisure.svg";
import goingToWorkIcon from "./icons/leisure-going-to-work.svg";
import goingToShopIcon from "./icons/leisure-shopping.svg";
import idlingIcon from "./icons/idling.svg";
import goingToLeisureIcon from "./icons/going-to-leisure.svg";
import notAnotherLeisureIcon from "./icons/not-another-leisure.svg";
import notShoppingIcon from "./icons/not-shopping.svg";
import leisureDesire1 from "./icons/leisure-desire1.svg";
import leisureDesire2 from "./icons/leisure-desire2.svg";
import leisureDesire3 from "./icons/leisure-desire3.svg";
import leisureDesire4 from "./icons/leisure-desire4.svg";
import leisureDesire5 from "./icons/leisure-desire5.svg";
import wealthWretched from "./icons/wealth-wretched.svg";
import wealthPoor from "./icons/wealth-poor.svg";
import wealthModest from "./icons/wealth-modest.svg";
import wealthComfortable from "./icons/wealth-comfortable.svg";
import wealthWealthy from "./icons/wealth-wealthy.svg";
import ageChild from "./icons/age-child.svg";
import ageTeen from "./icons/age-teen.svg";
import ageAdult from "./icons/age-adult.svg";
import ageElderly from "./icons/age-elderly.svg";

import "./CustomersTab.css";

enum CustomerStatus {
    None = 0,
    Leisuring = 1,
    GoingToLeisure = 2,
    Idling = 3,
    GoingToHome = 4,
    GoingToAnotherLeisure = 5,
    GoingToWork = 6,
    GoingToShop = 7,
    NotAnotherLeisure = 8,
    NotShopping = 9
}

enum Desire {
    Desire1 = 1,
    Desire2 = 2,
    Desire3 = 3,
    Desire4 = 4,
    Desire5 = 5,
}

enum Age {
    Child = 0,
    Teen = 1,
    Adult = 2,
    Elderly = 3
}

enum Wealth {
    Wretched = 0,
    Poor = 1,
    Modest = 2,
    Comfortable = 3,
    Wealthy = 4
}

type Entity = {
    index: number;
    version: number;
};


type Customer = {
    entity: Entity;
    name: any;
    status: CustomerStatus;
    desire: Desire;
    wealth: Wealth;
    age: Age;
    leisureHours: string;
    resource: number;
    money: number;
    service: boolean;
};

const customers$ = bindValue<Customer[]>(
    "BitulaMod",
    "customers",
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

const statusIcons: Partial<Record<CustomerStatus, string>> = {
    [CustomerStatus.Leisuring]: leisuringIcon,
    [CustomerStatus.GoingToLeisure]: goingToLeisureIcon,
    [CustomerStatus.GoingToAnotherLeisure]: goingToAnotherLeisureIcon,
    [CustomerStatus.GoingToWork]: goingToWorkIcon,
    [CustomerStatus.GoingToHome]: goingHomeIcon,
    [CustomerStatus.GoingToShop]: goingToShopIcon,
    [CustomerStatus.NotAnotherLeisure]: notAnotherLeisureIcon,
    [CustomerStatus.NotShopping]: notShoppingIcon,
    [CustomerStatus.Idling]: idlingIcon,
};

const desireIcons: Record<Desire, string> = {
    [Desire.Desire1]: leisureDesire1,
    [Desire.Desire2]: leisureDesire2,
    [Desire.Desire3]: leisureDesire3,
    [Desire.Desire4]: leisureDesire4,
    [Desire.Desire5]: leisureDesire5
};

const ageIcons: Record<Age, string> = {
    [Age.Child]: ageChild,
    [Age.Teen]: ageTeen,
    [Age.Adult]: ageAdult,
    [Age.Elderly]: ageElderly
};

const wealthIcons: Record<Wealth, string> = {
    [Wealth.Wretched]: wealthWretched,
    [Wealth.Poor]: wealthPoor,
    [Wealth.Modest]: wealthModest,
    [Wealth.Comfortable]: wealthComfortable,
    [Wealth.Wealthy]: wealthWealthy
};

export const ACTIONS_SECTION = selectedInfo.SectionType.Actions;

const CustomerRow = ({ customer }: { customer: Customer }) => {
    const name = useLocalizedName(customer.name);
    const leisureHoursRef = useRef<string>("");

    if (customer.leisureHours) {
        leisureHoursRef.current = customer.leisureHours;
    }

    const leisureHours = customer.leisureHours || leisureHoursRef.current;

    return (
        <div
            className={`${householdStyles.item} customer-row`}
            style={{
                backgroundColor:
                    customer.resource === 0
                        ? "red"
                        : !customer.service
                            ? "orange"
                            : undefined
            }}
            onClick={() => selectedInfo.selectEntity(customer.entity)}
        >
            <Avatar
                entity={customer.entity}
                className={householdStyles.avatar}
            />

            <div className={householdStyles.itemLabel}>
                {name}
            </div>

            {leisureHours && (
                <span className="customer-leisure-hours">
                    [{leisureHours}]
                </span>
            )}           

            <img
                src={desireIcons[customer.desire]}
                className="customer-desire-icon"
            />

            <img
                src={statusIcons[customer.status]}
                className="customer-status-icon"
            />

            <img
                src={ageIcons[customer.age]}
                className="customer-age-icon"
            />

            <Tooltip
                tooltip={
                    <LocalizedNumber
                        value={customer.money}
                        unit="money"
                    />
                }
            >
                <img
                    src={wealthIcons[customer.wealth]}
                    className="customer-wealth-icon"
                />
            </Tooltip>
        </div>
    );
};

export const CustomersPanel = () => {
    const customers = useValue(customers$);

    return (
        <>
            {customers.map((customer) => (
                <CustomerRow
                    key={`${customer.entity.index}:${customer.entity.version}`}
                    customer={customer}
                />
            ))}
        </>
    );
};

export const CustomersTab = (componentList: any): any => {
    

    const VanillaActionsSection =
        componentList[ACTIONS_SECTION];

    if (!VanillaActionsSection) {
        console.log("BitulaMod: ActionsSection not found");
        return componentList;
    }

    componentList[ACTIONS_SECTION] = (props: any) => {

        const selectedUITag = useValue(selectedInfo.selectedUITag$);
        const customers = useValue(customers$);
        const showCustomersTab = customers.length > 0;
        const [tabBar, setTabBar] = useState<Element | null>(null);
        const [customersSelected, setCustomersSelected] = useState(isCustomersTabSelected());

        useEffect(() => {
            const element = document.getElementsByClassName(panelStyles.tabBar)[0];
            if (element) {
                setTabBar(element);
            }
        }, []);

        useEffect(() => {
            return subscribeCustomersTab(setCustomersSelected);
        }, []);

        useEffect(() => {
            if (!showCustomersTab) {
                setCustomersTabSelected(false);
            }
        }, [showCustomersTab]);

        useEffect(() => {
            console.log("BitulaMod: selectedUITag =", selectedUITag);
        }, [selectedUITag]);       
        

        return (
            <>
                <VanillaActionsSection {...props} />

                {showCustomersTab && tabBar && createPortal(
                    <Tab
                        id={3}
                        selectedId={customersSelected ? 3 : 0}
                        onSelect={() => {
                            selectCustomTab("customers");
                        }}
                    >
                        <TintedIcon
                            src={customersIcon}
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