import { bindValue, useValue } from "cs2/api";
import { getModule } from "cs2/modding";

import {
    Children,
    cloneElement,
    isValidElement
} from "react";

const lastDayResourceCost$ = bindValue<number>(
    "BitulaMod",
    "lastDayResourceCost",
    0
);

const isHouseholdSelected$ = bindValue<boolean>(
    "BitulaMod",
    "isHouseholdSelected",
    false
);

const InfoRow: any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.tsx",
    "InfoRow"
);

const LocalizedNumber: any = getModule(
    "game-ui/common/localization/localized-number.tsx",
    "LocalizedNumber"
);

/*const Unit: any = getModule(
    "game-ui/common/localization/loc.generated.ts",
    "Unit"
);*/

export const ResidentsSection = (
    componentList: any
): any => {
    const RESIDENTS_SECTION =
        "Game.UI.InGame.ResidentsSection";


    const VanillaResidentsSection =
        componentList[RESIDENTS_SECTION];

    if (!VanillaResidentsSection) {
        console.log(
            "BitulaMod: vanilla ResidentsSection not found"
        );

        return componentList;
    }

    componentList[RESIDENTS_SECTION] = (props: any) => {
        const lastDayResourceCost =
            useValue(lastDayResourceCost$);

        const vanillaSection =
            VanillaResidentsSection(props);

        if (!isValidElement(vanillaSection)) {
            return vanillaSection;
        }

        const vanillaElement: any = vanillaSection;

        const sections: any[] = Children.toArray(
            vanillaElement.props.children
        );

        const wealthSection = sections[2];
        
        const isHouseholdSelected = useValue(isHouseholdSelected$);
        if (!isValidElement(wealthSection) || !isHouseholdSelected) {
            return vanillaSection;
        }

        const wealthElement: any = wealthSection;

        const wealthRows = Children.toArray(
            wealthElement.props.children
        );

        const previousDayRow = (
            <InfoRow
                key="last-day-resource-cost"
                left="Resource Cost (previous month)"
                right={
                    <>
                        <LocalizedNumber
                            value={lastDayResourceCost}
                            unit="money"
                        />
                        {" /mo."}
                    </>
                }
                tooltipKeys={props.tooltipKeys}
                tooltipTags={props.tooltipTags}
                disableFocus={true}
                subRow={true}
                uppercase={false}
            />
        );

        wealthRows.splice(
            wealthRows.length - 1,
            0,
            previousDayRow
        );

        const updatedWealthSection = cloneElement(
            wealthElement,
            {},
            ...wealthRows           
        );

        sections[2] = updatedWealthSection;

        return cloneElement(
            vanillaElement,
            {},
            ...sections
        );
    };

    return componentList;
};