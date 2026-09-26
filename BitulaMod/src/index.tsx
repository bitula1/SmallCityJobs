import { ModRegistrar } from "cs2/modding";
import { WorkShiftSection } from "mods/work-shift-section";
import { ResidentsSection } from "mods/resident-section";
import { useEffect, useState } from "react";
import { WorkersTab, WorkersPanel } from "mods/workers-tab/index";
import { CustomersTab, CustomersPanel } from "mods/customers-tab/index";


import {
    subscribeWorkersTab,
    isWorkersTabSelected,
    subscribeCustomersTab,
    isCustomersTabSelected,
    selectCustomTab
} from "mods/common/state";


const register: ModRegistrar = (moduleRegistry) => {
    console.log("BitulaMod: register called");
    
    moduleRegistry.extend(
        "game-ui/common/typed-renderer/typed-renderer.tsx",
        "TypedListRenderer",
        (Original) => {
            return (props: any) => {

                const [workersSelected, setWorkersSelected] =
                    useState(isWorkersTabSelected());

                const [customersSelected, setCustomersSelected] =
                    useState(isCustomersTabSelected());

                useEffect(() => {
                    return subscribeWorkersTab(setWorkersSelected);
                }, []);

                useEffect(() => {
                    return subscribeCustomersTab(setCustomersSelected);
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

                if (customersSelected && isSelectedInfoMiddleList) {
                    return <CustomersPanel />;
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

                const [customersSelected, setCustomersSelected] =
                    useState(isCustomersTabSelected());

                useEffect(() => {
                    return subscribeWorkersTab(setWorkersSelected);
                }, []);

                useEffect(() => {
                    return subscribeCustomersTab(setCustomersSelected);
                }, []);

                const nativeSelectedInfoTab =
                    props.id === 0 || props.id === 1;

                return (
                    <Original
                        {...props}
                        selectedId={
                            (workersSelected || customersSelected) && nativeSelectedInfoTab
                                ? -1
                                : props.selectedId
                        }
                        onSelect={(id: number) => {
                            if (nativeSelectedInfoTab) {
                                selectCustomTab(null);
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
            CustomersTab(componentList);
        } catch (error) {
            console.error(
                "BitulaMod: CustomersTab failed:",
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