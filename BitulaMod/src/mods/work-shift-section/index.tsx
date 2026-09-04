import { bindValue, useValue } from "cs2/api";
import { SectionType } from "cs2/bindings";
import { getModule } from "cs2/modding";

const workHours$ = bindValue<string>(
    "BitulaMod",
    "workHours",
    ""
);

const InfoSection: any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.tsx",
    "InfoSection"
);

const InfoRow: any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.tsx",
    "InfoRow"
);

export const WorkShiftSection = (componentList: any): any => {
    const CITIZEN_SECTION = "Game.UI.InGame.CitizenSection";

    console.log(
        "BitulaMod: Citizen exists:",
        Boolean(componentList[CITIZEN_SECTION])
    );

    const VanillaCitizenSection =
        componentList[CITIZEN_SECTION];

    if (!VanillaCitizenSection) {
        console.log("BitulaMod: vanilla CitizenSection not found");
        return componentList;
    }

    componentList[CITIZEN_SECTION] = (props: any) => {
        const workHours = useValue(workHours$);

        return (
            <>
                <VanillaCitizenSection {...props} />

                {workHours && (
                    <InfoSection disableFocus={true}>
                        <InfoRow
                            left="Work hours"
                            right={workHours}
                            tooltipKeys={props.tooltipKeys}
                            tooltipTags={props.tooltipTags}
                            disableFocus={true}
                            subRow={false}
                            uppercase={false}
                        />
                    </InfoSection>
                )}
            </>
        );
    };

    return componentList;
};