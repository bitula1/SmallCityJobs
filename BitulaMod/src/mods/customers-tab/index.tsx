import { selectedInfo } from "cs2/bindings";
import { getModule } from "cs2/modding";
import { useEffect, useState, useRef } from "react";
import { createPortal } from "react-dom";
import { bindValue, useValue } from "cs2/api";
import { setCustomersTabSelected, subscribeCustomersTab, isCustomersTabSelected, selectCustomTab } from "mods/common/state";
import customersIcon from "./icons/customers-group.svg";
import leisuringIcon from "./icons/leisuring.svg";
import goingToLeisureIcon from "./icons/going-to-leisure.svg";
import "./CustomersTab.css";

enum CustomerStatus {
    None = 0,
    Leisuring = 1,
    GoingToLeisure = 2
}

type Entity = {
    index: number;
    version: number;
};


type Customer = {
    entity: Entity;
    name: any;
    status: CustomerStatus;
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

const statusIcons: Partial<Record<CustomerStatus, string>> = {
    [CustomerStatus.Leisuring]: leisuringIcon,
    [CustomerStatus.GoingToLeisure]: goingToLeisureIcon
};

export const ACTIONS_SECTION = selectedInfo.SectionType.Actions;

const CustomerRow = ({ customer }: { customer: Customer }) => {
    const name = useLocalizedName(customer.name);

    return (
        <div
            className={`${householdStyles.item} customer-row`}
            onClick={() => selectedInfo.selectEntity(customer.entity)}
        >
            <Avatar
                entity={customer.entity}
                className={householdStyles.avatar}
            />

            <div className={householdStyles.itemLabel}>
                {name}
            </div>   

            <img
                src={statusIcons[customer.status]}
                className="customer-status-icon"
            />
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