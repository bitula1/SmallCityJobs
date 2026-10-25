import { bindValue, useValue } from "cs2/api";
import { SectionType } from "cs2/bindings";
import { getModule } from "cs2/modding";

const workHours$ = bindValue<string>(
    "BitulaMod",
    "workHours",
    ""
);

const sleepHours$ = bindValue<string>(
    "BitulaMod",
    "sleepHours",
    ""
);

const shoppingHours$ = bindValue<string>(
    "BitulaMod",
    "shoppingHours",
    ""
);

const leisureHours$ = bindValue<string>(
    "BitulaMod",
    "leisureHours",
    ""
);

const isDaysOff$ = bindValue<boolean>(
    "BitulaMod",
    "isDaysOff",
    false
);

const remainingDaysOff$ = bindValue<number>(
    "BitulaMod",
    "remainingDaysOff",
    0
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
        const sleepHours = useValue(sleepHours$);
        const shoppingHours = useValue(shoppingHours$);
        const leisureHours = useValue(leisureHours$);
        const isDaysOff = useValue(isDaysOff$);
        const remainingDaysOff = useValue(remainingDaysOff$);

        const daysOffText = remainingDaysOff > 0
            ? `Today (${remainingDaysOff} more day${remainingDaysOff === 1 ? "" : "s"})`
            : "Today";

        return (
            <>
                <VanillaCitizenSection {...props} />

                {(workHours || sleepHours || shoppingHours || leisureHours || isDaysOff) && (
                    <InfoSection disableFocus={true}>
                        {workHours && (
                            <div style={{ fontFamily: "monospace" }}>
                                <InfoRow
                                    left="Work hours"
                                    right={workHours}
                                    tooltipKeys={props.tooltipKeys}
                                    tooltipTags={props.tooltipTags}
                                    disableFocus={true}
                                    subRow={false}
                                    uppercase={false}
                                />
                            </div>
                        )}

                        {shoppingHours && (
                            <div style={{ fontFamily: "monospace" }}>
                                <InfoRow
                                    left="Shopping hours"
                                    right={shoppingHours}
                                    tooltipKeys={props.tooltipKeys}
                                    tooltipTags={props.tooltipTags}
                                    disableFocus={true}
                                    subRow={false}
                                    uppercase={false}
                                />
                            </div>
                        )}

                        {leisureHours && (
                            <div style={{ fontFamily: "monospace" }}>
                                <InfoRow
                                    left="Leisure hours"
                                    right={leisureHours}
                                    tooltipKeys={props.tooltipKeys}
                                    tooltipTags={props.tooltipTags}
                                    disableFocus={true}
                                    subRow={false}
                                    uppercase={false}
                                />
                            </div>
                        )}

                        {sleepHours && (
                            <div style={{ fontFamily: "monospace" }}>
                                <InfoRow
                                    left="Sleep hours"
                                    right={sleepHours}
                                    tooltipKeys={props.tooltipKeys}
                                    tooltipTags={props.tooltipTags}
                                    disableFocus={true}
                                    subRow={false}
                                    uppercase={false}
                                />
                            </div>
                        )}

                        {isDaysOff && (
                            <InfoRow
                                left="Days off"
                                right={daysOffText}
                                tooltipKeys={props.tooltipKeys}
                                tooltipTags={props.tooltipTags}
                                disableFocus={true}
                                subRow={false}
                                uppercase={false}
                            />
                        )}
                    </InfoSection>
                )}
            </>
        );
    };

    return componentList;
};