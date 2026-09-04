import { bindValue, useValue } from "cs2/api";

const workHours$ = bindValue<string>(
    "BitulaMod",
    "workHours",
    ""
);

export const HelloWorldComponent = () => {
    const workHours = useValue(workHours$);

    console.log("BitulaMod work hours:", workHours);

    return null;
};