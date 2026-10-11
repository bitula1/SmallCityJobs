import wealthWretched from "./icons/wealth-wretched.svg";
import wealthPoor from "./icons/wealth-poor.svg";
import wealthModest from "./icons/wealth-modest.svg";
import wealthComfortable from "./icons/wealth-comfortable.svg";
import wealthWealthy from "./icons/wealth-wealthy.svg";
import ageChild from "./icons/age-child.svg";
import ageTeen from "./icons/age-teen.svg";
import ageAdult from "./icons/age-adult.svg";
import ageElderly from "./icons/age-elderly.svg";
import family1 from "./icons/family-size-1.svg";
import family2 from "./icons/family-size-2.svg";
import family3 from "./icons/family-size-3.svg";
import family4 from "./icons/family-size-4.svg";
import family5 from "./icons/family-size-5.svg";

export enum Age {
    Child = 0,
    Teen = 1,
    Adult = 2,
    Elderly = 3
}

export enum Wealth {
    Wretched = 0,
    Poor = 1,
    Modest = 2,
    Comfortable = 3,
    Wealthy = 4
}

export enum Family {
    Family1 = 1,
    Family2 = 2,
    Family3 = 3,
    Family4 = 4,
    Family5 = 5,
}

export const ageIcons: Record<Age, string> = {
    [Age.Child]: ageChild,
    [Age.Teen]: ageTeen,
    [Age.Adult]: ageAdult,
    [Age.Elderly]: ageElderly
};

export const wealthIcons: Record<Wealth, string> = {
    [Wealth.Wretched]: wealthWretched,
    [Wealth.Poor]: wealthPoor,
    [Wealth.Modest]: wealthModest,
    [Wealth.Comfortable]: wealthComfortable,
    [Wealth.Wealthy]: wealthWealthy
};

export const familyIcons: Record<Family, string> = {
    [Family.Family1]: family1,
    [Family.Family2]: family2,
    [Family.Family3]: family3,
    [Family.Family4]: family4,
    [Family.Family5]: family5,
};

let workersSelectedGlobal = false;
let customersSelectedGlobal = false;
let shoppersSelectedGlobal = false;

const workersTabListeners = new Set<(selected: boolean) => void>();
const customersTabListeners = new Set<(selected: boolean) => void>();
const shoppersTabListeners = new Set<(selected: boolean) => void>();

export const setWorkersTabSelected = (selected: boolean) => {
    workersSelectedGlobal = selected;
    workersTabListeners.forEach(listener => listener(selected));
};

export const subscribeWorkersTab = (listener: (selected: boolean) => void) => {
    workersTabListeners.add(listener);
    return () => {
        workersTabListeners.delete(listener);
    };
};

export const isWorkersTabSelected = () => workersSelectedGlobal;

export const setCustomersTabSelected = (selected: boolean) => {
    customersSelectedGlobal = selected;
    customersTabListeners.forEach(listener => listener(selected));
};

export const subscribeCustomersTab = (listener: (selected: boolean) => void) => {
    customersTabListeners.add(listener);
    return () => {
        customersTabListeners.delete(listener);
    };
};

export const isCustomersTabSelected = () => customersSelectedGlobal;

export const setShoppersTabSelected = (selected: boolean) => {
    shoppersSelectedGlobal = selected;
    shoppersTabListeners.forEach(listener => listener(selected));
};

export const subscribeShoppersTab = (listener: (selected: boolean) => void) => {
    shoppersTabListeners.add(listener);
    return () => {
        shoppersTabListeners.delete(listener);
    };
};

export const isShoppersTabSelected = () => shoppersSelectedGlobal;

export type CustomTab = "workers" | "customers" | "shoppers" | null;

export const selectCustomTab = (tab: CustomTab) => {
    const workersSelected = tab === "workers";
    const customersSelected = tab === "customers";
    const shoppersSelected = tab === "shoppers";

    workersSelectedGlobal = workersSelected;
    customersSelectedGlobal = customersSelected;
    shoppersSelectedGlobal = shoppersSelected;

    workersTabListeners.forEach(listener => listener(workersSelected));
    customersTabListeners.forEach(listener => listener(customersSelected));
    shoppersTabListeners.forEach(listener => listener(shoppersSelected));
};