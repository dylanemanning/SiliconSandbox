# TC-6.2: Inventory items can be moved to the hotbar and placed

**Test Description:** Open the inventory, assign components to hotbar slots, and confirm the assigned component appears in the hotbar (TC-6.2.1) and can then be placed in the sandbox (TC-6.2.2).  
**Module:** Inventory (SR-6.2)  
**Priority:** High  
**Pre-conditions:** A build from `dev` (Windows or macOS) or the Unity Editor. A new, empty world. Default controls (E opens the inventory, 1-9 select hotbar slots).  

### Test Steps
**TC-6.2.1 – Assign to the hotbar**
1. Start → New Project, name the world `TC-6.2`, and create it. Screenshot the default hotbar.
2. Press E to open the inventory. Hover the XNOR gate; confirm it highlights and a hint names the component and the number keys.
3. While hovering, press 3. Confirm hotbar slot 3 now shows the XNOR image and the hint names slot 3.
4. Hover the Clock and press 9. Confirm slot 9 shows the Clock and slot 3 is unchanged.
5. Hover an empty inventory cell and press 4. Confirm slot 4 does not change.
6. Confirm the inventory still lists every component after the assignments. Screenshot the inventory and hotbar.

**TC-6.2.2 – Place the assigned component**

7. Press E to close the inventory.
8. Press 9 to select the Clock slot, aim at the ground and right click. Confirm a Clock is placed and it alternates on/off.
9. Press 3, aim at the ground beside the Clock and right click. Confirm an XNOR gate is placed. Screenshot the result.

### Expected Results
- **TC-6.2.1:** The hovered component is highlighted with a hint. The chosen slot shows the component's image, other slots are unchanged, empty cells assign nothing, and the inventory keeps every component.
- **TC-6.2.2:** After the inventory closes, the assigned slots select and place the correct components on the grid with the normal placement control.

### Evidence
Add screenshots under `Evidence/TC-6.2/` and reference them here after the test is run, for example:
- `![Inventory open with XNOR hovered and the assignment hint](Evidence/TC-6.2/assign.png)`
- `![Hotbar showing XNOR in slot 3 and Clock in slot 9](Evidence/TC-6.2/hotbar.png)`
- `![Clock and XNOR gate placed from the assigned slots](Evidence/TC-6.2/placed.png)`

### Test Log  
|Tester Name|Test Date|Success?|Additional Comments|
|---|---|---|---|
