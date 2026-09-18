import { ModRegistrar } from "cs2/modding";
import { WorkShiftSection } from "mods/work-shift-section";
import { ResidentsSection } from "mods/resident-section";
import { useEffect, useState } from "react";
import { WorkersTab, WorkersPanel } from "mods/workers-tab/index";


import {
    setWorkersTabSelected,
    subscribeWorkersTab,
    isWorkersTabSelected
} from "mods/workers-tab/state";


const register: ModRegistrar = (moduleRegistry) => {
    console.log("BitulaMod: register called");
    
    moduleRegistry.extend(
        "game-ui/common/typed-renderer/typed-renderer.tsx",
        "TypedListRenderer",
        (Original) => {
            return (props: any) => {                

                const [workersSelected, setWorkersSelected] =
                    useState(isWorkersTabSelected());

                useEffect(() => {
                    return subscribeWorkersTab(setWorkersSelected);
                }, []);

                const isSelectedInfoMiddleList =
                    Array.isArray(props.data) &&
                    props.data.some(
                        (item: any) =>
                            item?.__Type === "Game.UI.InGame.EmployeesSection" ||
                            item?.__Type === "Game.UI.InGame.VisualCustomizeSection"
                    );

                if (workersSelected && isSelectedInfoMiddleList) {
                    return <WorkersPanel />;
                }

                return <Original {...props} />;
            };
        }
    );

    moduleRegistry.extend(
        "game-ui/common/tabs/tabs.tsx",
        "Tab",
        (Original) => {
            return (props: any) => {
                const [workersSelected, setWorkersSelected] =
                    useState(isWorkersTabSelected());

                useEffect(() => {
                    return subscribeWorkersTab(setWorkersSelected);
                }, []);

                const nativeSelectedInfoTab =
                    props.id === 0 || props.id === 1;

                return (
                    <Original
                        {...props}
                        selectedId={
                            workersSelected && nativeSelectedInfoTab
                                ? -1
                                : props.selectedId
                        }
                        onSelect={(id: number) => {
                            if (nativeSelectedInfoTab) {
                                setWorkersTabSelected(false);
                            }

                            props.onSelect?.(id);
                        }}
                    />
                );
            };
        }
    );

    moduleRegistry.extend(
    "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx",
    "selectedInfoSectionComponents",
    (componentList: any) => {
        console.log("BitulaMod: inline extender called");

        try {
            WorkersTab(componentList);
        } catch (error) {
            console.error(
                "BitulaMod: WorkersTab failed:",
                error
            );
        }

        try {
            WorkShiftSection(componentList);
        } catch (error) {
            console.error(
                "BitulaMod: WorkShiftSection failed:",
                error
            );
        }

        try {
            ResidentsSection(componentList);
        } catch (error) {
            console.error(
                "BitulaMod: ResidentsSection failed:",
                error
            );
        }

        return componentList;
    });
};

export default register;