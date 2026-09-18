import { selectedInfo } from "cs2/bindings";
import { getModule } from "cs2/modding";
import { useEffect, useState, useRef } from "react";
import { createPortal } from "react-dom";
//import { bindValue, bindMap, useValue, useMapValue} from "cs2/api";
import { bindValue, useValue } from "cs2/api";
import { setWorkersTabSelected, subscribeWorkersTab, isWorkersTabSelected } from "mods/workers-tab/state";

type Entity = {
    index: number;
    version: number;
};


type Worker = {
    entity: Entity;
    name: any;
};


/*const workers$ = bindValue<Entity[]>(
    "BitulaMod",
    "workers",
    []
);*/
const workers$ = bindValue<Worker[]>(
    "BitulaMod",
    "workers",
    []
);

/*const avatars$ = bindMap<any, any>(
    "avatars",
    "avatarsMap"
);*/


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


export const ACTIONS_SECTION = selectedInfo.SectionType.Actions;

const WorkerRow = ({ worker }: { worker: Worker }) => {
    const name = useLocalizedName(worker.name);

    return (
        <div
            className={householdStyles.item}
            onClick={() => selectedInfo.selectEntity(worker.entity)}
        >
            <Avatar
                entity={worker.entity}
                className={householdStyles.avatar}
            />

            <div className={householdStyles.itemLabel}>
                {name}
            </div>
        </div>
    );
};

export const WorkersPanel = () => {
    const workers = useValue(workers$);

    return (
        <>
            {workers.map((worker) => (
                <WorkerRow
                    key={`${worker.entity.index}:${worker.entity.version}`}
                    worker={worker}
                />
            ))}
        </>
    );
};

export const WorkersTab = (componentList: any): any => {
    

    const VanillaActionsSection =
        componentList[ACTIONS_SECTION];

    if (!VanillaActionsSection) {
        console.log("BitulaMod: ActionsSection not found");
        return componentList;
    }

    componentList[ACTIONS_SECTION] = (props: any) => {

        const selectedUITag = useValue(selectedInfo.selectedUITag$);
        const workers = useValue(workers$);
        const showWorkersTab = workers.length > 0;
        const [tabBar, setTabBar] = useState<Element | null>(null);
        const [workersSelected, setWorkersSelected] = useState(isWorkersTabSelected());

        useEffect(() => {
            const element = document.getElementsByClassName(panelStyles.tabBar)[0];
            if (element) {
                setTabBar(element);
            }
        }, []);

        useEffect(() => {
            return subscribeWorkersTab(setWorkersSelected);
        }, []);

        useEffect(() => {
            if (!showWorkersTab) {
                setWorkersTabSelected(false);
            }
        }, [showWorkersTab]);

        useEffect(() => {
            console.log("BitulaMod: selectedUITag =", selectedUITag);
        }, [selectedUITag]);       
        

        return (
            <>
                <VanillaActionsSection {...props} />

                {showWorkersTab && tabBar && createPortal(
                    <Tab
                        id={2}
                        selectedId={workersSelected ? 2 : 0}
                        onSelect={() => {
                            setWorkersTabSelected(true);
                        }}
                    >
                        <TintedIcon
                            src="Media/Game/Icons/Citizen.svg"
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