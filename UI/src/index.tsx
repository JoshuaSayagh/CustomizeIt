import { ModRegistrar } from "cs2/modding";
import { AttractivenessPanel } from "mods/attractiveness-panel";

const register: ModRegistrar = (moduleRegistry) => {
    // Append our panel to the in-game UI so it renders during gameplay
    moduleRegistry.append('Game', AttractivenessPanel);
}

export default register;
