import { selectedInfo } from "cs2/bindings";
import { getModule } from "cs2/modding";
import { useEffect, useState, useRef } from "react";
import { createPortal } from "react-dom";
import { bindValue, useValue } from "cs2/api";
import { setWorkersTabSelected, subscribeWorkersTab, isWorkersTabSelected } from "mods/workers-tab/state";
import workingIcon from "./icons/working.svg";
import notWorkingIcon from "./icons/not-working.svg";
import dayOffIcon from "./icons/day-off.svg";
import goingToWorkIcon from "./icons/going-to-work.svg";
import employerGoneIcon from "./icons/employer-gone.svg";
import workplaceGoneIcon from "./icons/workplace-gone.svg";
import workingElsewhereIcon from "./icons/working-elsewhere.svg";
import "./WorkersTab.css";

enum WorkerStatus {
    None = 0,
    Working = 1,
    NotWorking = 2,
    DayOff = 3,
    GoingToWork = 4,
    EmployerGone = 5,
    WorkplaceGone = 6,
    WorkingElsewhere = 7
}

const statusIcons: Partial<Record<WorkerStatus, string>> = {
    [WorkerStatus.Working]: workingIcon,
    [WorkerStatus.NotWorking]: notWorkingIcon,
    [WorkerStatus.DayOff]: dayOffIcon,
    [WorkerStatus.GoingToWork]: goingToWorkIcon,
    [WorkerStatus.EmployerGone]: employerGoneIcon,
    [WorkerStatus.WorkplaceGone]: workplaceGoneIcon,
    [WorkerStatus.WorkingElsewhere]: workingElsewhereIcon
};

type Entity = {
    index: number;
    version: number;
};


type Worker = {
    entity: Entity;
    name: any;
    status: WorkerStatus;
};

const workers$ = bindValue<Worker[]>(
    "BitulaMod",
    "workers",
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


export const ACTIONS_SECTION = selectedInfo.SectionType.Actions;

const WorkerRow = ({ worker }: { worker: Worker }) => {
    const name = useLocalizedName(worker.name);

    return (
        <div
            className={`${householdStyles.item} worker-row`}
            onClick={() => selectedInfo.selectEntity(worker.entity)}
        >
            <Avatar
                entity={worker.entity}
                className={householdStyles.avatar}
            />

            <div className={householdStyles.itemLabel}>
                {name}
            </div>

            <img
                src={statusIcons[worker.status]}
                className="worker-status-icon"
            />
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