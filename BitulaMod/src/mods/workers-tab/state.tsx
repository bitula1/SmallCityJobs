
let workersSelectedGlobal = false;
let customersSelectedGlobal = false;

const workersTabListeners = new Set<(selected: boolean) => void>();
const customersTabListeners = new Set<(selected: boolean) => void>();

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

export const isCustomersTabSelected = () => workersSelectedGlobal;