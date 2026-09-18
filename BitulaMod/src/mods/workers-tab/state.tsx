
let workersSelectedGlobal = false;

const workersTabListeners = new Set<(selected: boolean) => void>();

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

/*export const WORKER_BUILDING_PREFIXES = [
    "BuildingPrefab:IndustrialManufacturing",
    "BuildingPrefab:IndustrialAgricultureHub",
    "BuildingPrefab:IndustrialAquacultureLandHub",
    "BuildingPrefab:IndustrialForestryHub",
    "BuildingPrefab:IndustrialOreHub",
    "BuildingPrefab: EU_CommercialLow"
];*/