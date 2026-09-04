import { ModRegistrar } from "cs2/modding";
import { WorkShiftSection } from "mods/work-shift-section";
import { ResidentsSection } from "mods/resident-section";

const register: ModRegistrar = (moduleRegistry) => {
    console.log("BitulaMod: register called");

    moduleRegistry.extend(
    "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx",
    "selectedInfoSectionComponents",
    (componentList: any) => {
        console.log("BitulaMod: inline extender called");

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