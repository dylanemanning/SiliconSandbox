# Verification Test Inventory

| Test Case ID | Level (Unit/Integration/System) | Description | Req. ID | Test Owner | Tool | Automated? | CI Integrated? | Evidence Link |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| TC-1.1.1 | Unit | Create a project | SR-1.1 | Dylan | Unity Test Framework, System.IO | Yes | not completed yet | 9/27 |
| TC-1.1.2 | Unit | Create multiple projects | SR-1.1 | Dylan | Unity Test Framework, System.IO | Yes | not completed yet | 9/27 |
| TC-1.1.3 | Unit | Verify creating a new project brings user into same environment each time | SR-1.1 | Dylan | Unity Test Framework | Yes | not completed yet | 9/27 |
| TC-1.1 | Integration | Can create new project | SR-1.1 | Dylan | Unity Test Framework, System.IO | Yes | not completed yet | 9/27 |
| TC-1.2.1 | Unit | Save a project | SR-1.2 | Dylan | Unity Test Framework, System.IO | Yes | not completed yet | 9/27 |
| TC-1.2.2 | Unit | Name & Save a project | SR-1.2 | Dylan | Unity Test Framework, System.IO | Yes | not completed yet | 9/27 |
| TC-1.2.3 | Unit | View a list of existing projects | SR-1.2 | Dylan | Unity Test Framework | Yes | not completed yet | 9/27 |
| TC-1.2.4 | Unit | After saving a project, the game files for said project can be found in the computer's file system | SR-1.2 | Dylan | Unity Test Framework, System.IO | Yes | not completed yet | 9/27 |
| TC-1.2.5 | Unit | Save a project with a duplicate name, it is rejected | SR-1.2 | Dylan | Unity Test Framework, System.IO | Yes | not completed yet | 9/27 |
| TC-1.2 | Integration | Can hold multiple existing projects | SR-1.2 | Dylan | Unity Test Framework, System.IO | Yes | not completed yet | 9/27 |
| TC-1.3.1 | Unit | Delete a project, its name does not reappear in the list of saved projects | SR-1.3 | Dylan | Unity Test Framework | Yes | not completed yet | 10/4 |
| TC-1.3.2 | Unit | Attempt to delete a project, a confirmation message appears | SR-1.3 | Dylan | Unity Test Framework, Unity UI | Partial | not completed yet | 10/4 |
| TC-1.3.3 | Unit | Delete a project, it no longer takes up space in the game files | SR-1.3 | Dylan | Unity Test Framework, System.IO | Yes | not completed yet | 10/4 |
| TC-1.3 | Integration | Can delete selected project | SR-1.3 | Dylan | Unity Test Framework, System.IO | Yes | not completed yet | 10/4 |
| TC-1.4.1 | Unit | Click on a blank save, the default world loads | SR-1.4 | Dylan | Unity Test Framework | Yes | not completed yet | 9/27 |
| TC-1.4.2 | Unit | Click on a save with a basic circuit, all connections remain as they were | SR-1.4 | Dylan | Unity Test Framework | Yes | not completed yet | 9/27 |
| TC-1.4.3 | Unit | Click on a save with a basic circuit, the circuit retains it's logical behavior | SR-1.4 | Dylan | Unity Test Framework | Yes | not completed yet | 9/27 |
| TC-1.4 | Integration | Can open existing save | SR-1.4 | Dylan | Unity Test Framework, System.IO | Yes | not completed yet | 9/27 |
| TC-1 | System | Project Selection | PR-1 | Dylan | Unity Test Framework, System.IO, System.Diagnostics.Stopwatch | Partial | not completed yet | 9/27 |
| TC-2.1.1 | Unit | Create a backup for an existing project | SR-2.1 | Gray | Unity Test Framework, System.IO | Yes | not completed yet | 11/7 |
| TC-2.1.2 | Unit | Create multiple backups for the same project | SR-2.1 | Gray | Unity Test Framework, System.IO | Yes | not completed yet | 11/7 |
| TC-2.1.3 | Unit | Verify all created backups appear in the project's backup list with timestamps | SR-2.1 | Gray | Unity Test Framework, System.IO | Yes | not completed yet | 11/7 |
| TC-2.1 | Integration | can hold multiple backups | SR-2.1 | Gray | Unity Test Framework, System.IO | Yes | not completed yet | 11/7 |
| TC-2.2.1 | Unit | Select and restore an existing backup, project state reverts to that backup | SR-2.2 | Gray | Unity Test Framework, System.IO | Yes | not completed yet | 11/7 |
| TC-2.2.2 | Unit | Verify circuit components and connections are restored accurately from retrieved backup | SR-2.2 | Gray | Unity Test Framework, System.IO | Yes | not completed yet | 11/7 |
| TC-2.2.3 | Unit | Attempt to retrieve a corrupted or missing backup, an error message appears | SR-2.2 | Gray | Unity Test Framework, System.IO | Yes | not completed yet | 11/7 |
| TC-2.2 | Integration | Backups can be retrieved | SR-2.2 | Gray | Unity Test Framework, System.IO | Yes | not completed yet | 11/7 |
| TC-2.3.1 | Unit | Select and delete a backup, verify it no longer appears in the backup list | SR-2.3 | Gray | Unity Test Framework, System.IO | Yes | not completed yet | 11/14 |
| TC-2.3.2 | Unit | Attempt to delete a backup, a confirmation message appears | SR-2.3 | Gray | Unity Test Framework, Unity UI | Partial | not completed yet | 11/14 |
| TC-2.3.3 | Unit | Delete a backup, verify associated files are removed from the file system | SR-2.3 | Gray | Unity Test Framework, System.IO | Yes | not completed yet | 11/14 |
| TC-2.3 | Integration | Backups can be removed | SR-2.3 | Gray | Unity Test Framework, System.IO | Yes | not completed yet | 11/14 |
| TC-2.4.1 | Unit | Create a new branch project from an existing backup point | SR-2.4 | Gray | Unity Test Framework, System.IO | Yes | not completed yet | 12/5 |
| TC-2.4.2 | Unit | Make changes to a branched project, verify original project and backups remain unaffected | SR-2.4 | Gray | Unity Test Framework, System.IO | Yes | not completed yet | 12/5 |
| TC-2.4.3 | Unit | Verify branching history shows the relationship between original project and branch | SR-2.4 | Gray | Unity Test Framework, System.IO | Yes | not completed yet | 12/5 |
| TC-2.4 | Integration | Backups can branch | SR-2.4 | Gray | Unity Test Framework, System.IO | Yes | not completed yet | 12/5 |
| TC-2 | System | Backups | PR-2 | Gray | Unity Test Framework, System.IO | Partial | not completed yet | 11/21 |
| TC-3.1.1 | Unit | Check netlist for premade components | SR-3.1 | Gray | Unity Test Framework | Yes | not completed yet | 10/9 |
| TC-3.1.2 | Unit | Check netlist for user made circuit | SR-3.1 | Gray | Unity Test Framework | Yes | not completed yet | 10/9 |
| TC-3.1.3 | Unit | Check netlist for packaged circuit | SR-3.1 | Gray | Unity Test Framework | Yes | not completed yet | 11/14 |
| TC-3.1.4 | Unit | Check that netlist updates accurately when circuit changes | SR-3.1 | Gray | Unity Test Framework | Yes | not completed yet | 10/9 |
| TC-3.1 | Integration | System generates accurate netlists | SR-3.1 | Gray | Unity Test Framework | Yes | not completed yet | 10/9 |
| TC-3.2.1 | Unit | Verify AND gate truth table | SR-3.2 | Gray | Unity Test Framework | Yes | not completed yet | 9/27 |
| TC-3.2.2 | Unit | Verify NAND gate truth table | SR-3.2 | Gray | Unity Test Framework | Yes | not completed yet | 10/16 |
| TC-3.2.3 | Unit | Verify OR gate truth table | SR-3.2 | Gray | Unity Test Framework | Yes | not completed yet | 9/27 |
| TC-3.2.4 | Unit | Verify NOR gate truth table | SR-3.2 | Gray | Unity Test Framework | Yes | not completed yet | 10/16 |
| TC-3.2.5 | Unit | Verify XOR gate truth table | SR-3.2 | Gray | Unity Test Framework | Yes | not completed yet | 10/16 |
| TC-3.2.6 | Unit | Verify XNOR gate truth table | SR-3.2 | Gray | Unity Test Framework | Yes | not completed yet | 10/16 |
| TC-3.2.7 | Unit | Verify NOT gate truth table | SR-3.2 | Gray | Unity Test Framework | Yes | not completed yet | 9/27 |
| TC-3.2.8 | Unit | Verify BUFFER gate truth table | SR-3.2 | Gray | Unity Test Framework | Yes | not completed yet | 10/16 |
| TC-3.2.9 | Unit | Basic gate + Not = !Basic gate | SR-3.2 | Gray | Unity Test Framework | Yes | not completed yet | 10/9 |
| TC-3.2 | Integration | Premade logic gates behave as expected | SR-3.2 | Gray | Unity Test Framework | Yes | not completed yet | 10/16 |
| TC-3.3.1 | Unit | Flip Flop can be created, and only changes on clock edge | SR-3.3 | Gray | Unity Test Framework | Yes | not completed yet | 11/14 |
| TC-3.3.2 | Unit | When clock is not changed, a clocked circuit stays static | SR-3.3 | Gray | Unity Test Framework | Yes | not completed yet | 11/14 |
| TC-3.3.3 | Unit | Creating a circuit with clocked elements behaves differently than combinational circuit with the same logic | SR-3.3 | Gray | Unity Test Framework | Yes | not completed yet | 11/14 |
| TC-3.3 | Integration | Clocked logic delays, unlike combinational logic | SR-3.3 | Gray | Unity Test Framework | Yes | not completed yet | 11/14 |
| TC-3.4.1 | Unit | Creating a positive signal at one end of a wire creates a positive signal at the other end | SR-3.4 | Gray | Unity Test Framework | Yes | Yes | https://github.com/dylanemanning/SiliconSandbox/blob/dev/Assets/Tests/Editor/LogicSignalRulesTests.cs |
| TC-3.4.2 | Unit | Creating a ground signal at one end of a wire creates a ground signal at the other end | SR-3.4 | Gray | Unity Test Framework | Yes | Yes | https://github.com/dylanemanning/SiliconSandbox/blob/dev/Assets/Tests/Editor/LogicSignalRulesTests.cs |
| TC-3.4.3 | Unit | Changing the input of a gate can change the output of the gate | SR-3.4 | Gray | Unity Test Framework | Yes | not completed yet | 9/27 |
| TC-3.4.4 | Unit | Toggling the signal at the end of a wire toggles the signal at the other end | SR-3.4 | Gray | Unity Test Framework | Yes | not completed yet | 10/16 |
| TC-3.4.5 | Unit | Driving both ends of a wire to different values creates and error | SR-3.4 | Gray | Unity Test Framework | Yes | not completed yet | 10/16 |
| TC-3.4.6 | Unit | Driving the output of a gate does not affect the input of the gate | SR-3.4 | Gray | Unity Test Framework | Yes | not completed yet | 10/16 |
| TC-3.4 | Integration | Signals propogate through wires and gates | SR-3.4 | Gray | Unity Test Framework | Partial | not completed yet | 10/16 |
| TC-3 | System | Accurate Logical Simulation | PR-3 | Gray | Unity Test Framework | Partial | not completed yet | 11/14 |
| TC-4.1.1 | Unit | Select one component with the visual tool and package it | SR-4.1 | Janis |  |  | not completed yet |  |
| TC-4.1.2 | Unit | Select multiple (3) gate w/ wires connecting them and package them | SR-4.1 | Janis |  |  | not completed yet |  |
| TC-4.1.3 | Unit | Select multiple (3) gate w/ wires connecting them and then unselect without packaging | SR-4.1 | Janis |  |  | not completed yet |  |
| TC-4.1.4 | Unit | Select multiple (3) gate w/ wires connecting them, package them, and then place the package | SR-4.1 | Janis |  |  | not completed yet |  |
| TC-4.1 | Integration | Multiple componenets can be selected and packaged | SR-4.1 | Janis |  |  | not completed yet |  |
| TC-4.2.1 | Unit | Create a package with multiple components, place it, open the package | SR-4.2 | Janis |  |  | not completed yet |  |
| TC-4.2.2 | Unit | Create a package, save&exit the project, reopen the project, expand the package, components should remain the same | SR-4.2 | Janis |  |  | not completed yet |  |
| TC-4.2.3 | Unit | Create a package, expand the packaged kmap without expanding the entire package | SR-4.2 | Janis |  |  | not completed yet |  |
| TC-4.2 | Integration | Packages can be expanded | SR-4.2 | Janis |  |  | not completed yet |  |
| TC-4.3.1 | Unit | Expand a package, change 1 component, close and save the package, reopen to verify component remains changed | SR-4.3 | Janis |  |  | not completed yet |  |
| TC-4.3.2 | Unit | Expand a package, change 1 component, save&exit, verify that package behavior has changed | SR-4.3 | Janis |  |  | not completed yet |  |
| TC-4.3.3 | Unit | Expand a package, change 1 component, save&exit, verify that package kmap has changed | SR-4.3 | Janis |  |  | not completed yet |  |
| TC-4.3.4 | Unit | Select package, rename it, check that name updates in inventory | SR-4.3 | Janis |  |  | not completed yet |  |
| TC-4.3 | Integration | Packages can be edited | SR-4.3 | Janis |  |  | not completed yet |  |
| TC-4.4.1 | Unit | Place a saved package from inventory into a circuit | SR-4.4 | Janis |  |  | not completed yet |  |
| TC-4.4.2 | Unit | Connect wires to inputs and outputs of placed package, verify signal propagation | SR-4.4 | Janis |  |  | not completed yet |  |
| TC-4.4.3 | Unit | Place multiple instances of the same package in a circuit, verify independent behavior | SR-4.4 | Janis |  |  | not completed yet |  |
| TC-4.4 | Integration | Packages can be placed in another circuit | SR-4.4 | Janis |  |  | not completed yet |  |
| TC-4.5.1 | Unit | Select a package and additional gates/wires, and create a nested package | SR-4.5 | Janis |  |  | not completed yet |  |
| TC-4.5.2 | Unit | Place nested package in a circuit, verify all inputs and outputs function properly | SR-4.5 | Janis |  |  | not completed yet |  |
| TC-4.5.3 | Unit | Expand nested package, verify internal hierarchy and sub-package components remain intact | SR-4.5 | Janis |  |  | not completed yet |  |
| TC-4.5 | Integration | A circuit containing a package can be packaged | SR-4.5 | Janis |  |  | not completed yet |  |
| TC-4.6.1 | Unit | Verify truth table of packaged circuit matches unpackaged version | SR-4.6 | Janis |  |  | not completed yet |  |
| TC-4.6.2 | Unit | Test all input combinations on a complex packaged circuit, verify expected outputs | SR-4.6 | Janis |  |  | not completed yet |  |
| TC-4.6.3 | Unit | Verify stateful logic inside package retains correct internal state across transitions | SR-4.6 | Janis |  |  | not completed yet |  |
| TC-4.6 | Integration | Packages keep logical accuracy | SR-4.6 | Janis |  |  | not completed yet |  |
| TC-4.7.1 | Unit | Verify propagation delay through package matches delay of unpackaged gates | SR-4.7 | Janis |  |  | not completed yet |  |
| TC-4.7.2 | Unit | Verify clocked logic within package triggers accurately on clock edge | SR-4.7 | Janis |  |  | not completed yet |  |
| TC-4.7.3 | Unit | Verify high-frequency clock signals propagate through package without timing skew or desync | SR-4.7 | Janis |  |  | not completed yet |  |
| TC-4.7 | Integration | Packages retain timing accuracy | SR-4.7 | Janis |  |  | not completed yet |  |
| TC-4 | System | Packaging | PR-4 | Janis |  |  | not completed yet |  |
| TC-5.1.1 | Unit | Verify that files all serialize into JSON | SR-5.1 | Dylan | Unity Test Framework, Newtonsoft.Json, System.IO | Yes | not completed yet | 10/11 |
| TC-5.1.2 | Unit | Verify that files can be named special characters | SR-5.1 | Dylan | Unity Test Framework, Newtonsoft.Json, System.IO | Yes | not completed yet | 10/11 |
| TC-5.1 | Integration | Serialization into standard format | SR-5.1 | Dylan | Unity Test Framework, Newtonsoft.Json, System.IO | Yes | not completed yet | 10/11 |
| TC-5.2.1 | Unit | Verify that changes to specific components stay the same when saving | SR-5.2 | Dylan | Unity Test Framework, Newtonsoft.Json, System.IO | Yes | not completed yet | 9/27 |
| TC-5.2.2 | Unit | Verify that state of logic gates holds consistent when saving and loading | SR-5.2 | Dylan | Unity Test Framework, Newtonsoft.Json, System.IO | Yes | not completed yet | 10/11 |
| TC-5.2 | Integration | Save files must include components | SR-5.2 | Dylan | Unity Test Framework, Newtonsoft.Json, System.IO | Yes | not completed yet | 10/11 |
| TC-5.3.1 | Unit | Verify that screenshot matches last view after saving | SR-5.3 | Dylan | Unity Test Framework, Unity ScreenCapture | No | not completed yet | 10/11 |
| TC-5.3.2 | Unit | Verify that screenshot does not match last view when not saving | SR-5.3 | Dylan | Unity Test Framework, Unity ScreenCapture | No | not completed yet | 10/11 |
| TC-5.3 | Integration | Save files must show correct screenshot | SR-5.3 | Dylan | Unity Test Framework, Unity ScreenCapture | No | not completed yet | 10/11 |
| TC-5 | System | Save/Load Functionality | PR-5 | Dylan | Unity Test Framework, Newtonsoft.Json, System.IO, Unity ScreenCapture | Partial | not completed yet | 10/11 |
| TC-6.1.1 | Unit | Verify that components are separated by category in the inverntory | SR-6.1 | Janis |  |  | not completed yet |  |
| TC-6.1.2 | Unit | Click through each tab and verify all components are findable | SR-6.1 | Janis |  |  | not completed yet |  |
| TC-6.1 | Integration | Components organized into categories | SR-6.1 | Janis |  |  | not completed yet |  |
| TC-6.2.1 | Unit | Click on an item or component in the inventory and move it into the hotbar, verify that it appears | SR-6.2 | Janis |  |  | not completed yet |  |
| TC-6.2.2 | Unit | Verify that once an item is moved form inventory to hotbar it can be placed in the sandbox | SR-6.2 | Janis |  |  | not completed yet |  |
| TC-6.2 | Integration | Move items from Inventory to hotbar | SR-6.2 | Janis |  |  | not completed yet |  |
| TC-6.3.1 | Unit | Verify that there exists a search bar in the top of the inventory | SR-6.3 | Janis |  |  | not completed yet |  |
| TC-6.3.2 | Unit | Click on the search bar and verify that it functions as expected | SR-6.3 | Janis |  |  | not completed yet |  |
| TC-6.3 | Integration | Search bar must be usable | SR-6.3 | Janis |  |  | not completed yet |  |
| TC-6 | System | Inventory | PR-6 | Janis |  |  | not completed yet |  |
| TC-7.1.1 | Unit | Verify that when placing a component, the component snaps into a grid of the world | SR-7.1 | Gray | Unity Test Framework (PlayMode) | Partial | not completed yet | 10/9 |
| TC-7.1.2 | Unit | Verify that if a component exists, another component cannot be placed over | SR-7.1 | Gray | Unity Test Framework (PlayMode) | Partial | not completed yet | 10/9 |
| TC-7.1 | Integration | Components snap into a grid | SR-7.1 | Gray | Unity Test Framework (PlayMode) | Partial | not completed yet | 10/9 |
| TC-7.2.1 | Unit | Place a component, place another one, connect the two with wires, ensure they snap correctly | SR-7.2 | Gray | Unity Test Framework (PlayMode) | Partial | not completed yet | 10/16 |
| TC-7.2.2 | Unit | Ensure that after wires are placed, the netlist generated shows the connections | SR-7.2 | Gray | Unity Test Framework | Yes | not completed yet | 10/16 |
| TC-7.2 | Integration | Wires can be drawn between inputs and outputs | SR-7.2 | Gray | Unity Test Framework (PlayMode) | Partial | not completed yet | 10/16 |
| TC-7.3.1 | Unit | Build a benchmark cuircuit, ensure once run that the output matches real output | SR-7.3 | Gray | Unity Test Framework | Yes | not completed yet | 10/16 |
| TC-7.3.2 | Unit | Ensure that once fully built, it can be run multiple times without fail | SR-7.3 | Gray | Unity Test Framework | Yes | not completed yet | 10/16 |
| TC-7.3 | Integration | Ciruits must simulate real circuits | SR-7.3 | Gray | Unity Test Framework | Yes | not completed yet | 10/16 |
| TC-7 | System | Circuit Building | PR-7 | Gray | Unity Test Framework (PlayMode) | Partial | not completed yet | 11/14 |
| TC-8.1.1 | Unit | Verify that each keybind follows the same mapping as minecraft and other popular games | SR-8.1 | Dylan | Unity Test Framework, Unity Input System | Yes | not completed yet | 10/4 |
| TC-8.1.2 | Unit | Verify that the player can move with wasd and can look with the camera | SR-8.1 | Dylan | Unity Test Framework, Unity Input System | Partial | not completed yet | 9/27 |
| TC-8.1 | Integration | Keybinds must have normal defaults | SR-8.1 | Dylan | Unity Test Framework, Unity Input System | Yes | not completed yet | 10/4 |
| TC-8.2.1 | Unit | Navigate to the setting page and view the defualt keybinds, change them and ensure that the change appears | SR-8.2 | Dylan | Unity Test Framework, Unity Input System | Partial | not completed yet | 10/4 |
| TC-8.2.2 | Unit | Ensure that once changed, changed keybinds hold their new key | SR-8.2 | Dylan | Unity Test Framework, Unity Input System | Yes | not completed yet | 10/4 |
| TC-8.2 | Integration | Controls must be editable | SR-8.2 | Dylan | Unity Test Framework, Unity Input System | Yes | not completed yet | 10/4 |
| TC-8.3.1 | Unit | Test looking around in a sandbox environment and that moving the mouse corresponds to camera movement | SR-8.3 | Dylan | Unity Test Framework, Unity Input System | Partial | not completed yet | 9/27 |
| TC-8.3.2 | Unit | Ensure that in the settings page the camera sensitivity and x and y axises can be changed | SR-8.3 | Dylan | Unity Test Framework, Unity Input System | Partial | not completed yet | 10/11 |
| TC-8.3 | Integration | Camera look around feels natural | SR-8.3 | Dylan | Unity Test Framework, Unity Input System | Partial | Yes | https://github.com/dylanemanning/SiliconSandbox/blob/dev/doc/TestDocs/TC-8.3_CameraFeelsNatural.md |
| TC-8 | System | User Controls | PR-8 | Dylan | Unity Test Framework, Unity Input System | Partial | not completed yet | 10/11 |
| TC-9.1.1 | Unit | Verify that when entering the game the first thing the user sees is the main menu | SR-9.1 | Dylan | Unity Test Framework, Unity SceneManager | Yes | not completed yet | 9/27 |
| TC-9.1.2 | Unit | Verify that when the user exists a save file and returns to the main menu, the main menu appears | SR-9.1 | Dylan | Unity Test Framework, Unity SceneManager | Yes | not completed yet | 9/27 |
| TC-9.1 | Integration | Main menu screen exists | SR-9.1 | Dylan | Unity Test Framework, Unity SceneManager | Yes | not completed yet | 9/27 |
| TC-9.2.1 | Unit | Verify that when inside the sandbox environment the user can click escape and pull up the pause menu | SR-9.2 | Dylan | Unity Test Framework, Unity Input System | Yes | not completed yet | 9/27 |
| TC-9.2.2 | Unit | Ensure that when the pause menu is up, the screens are navigable and the settings page exists | SR-9.2 | Dylan | Unity Test Framework, Unity Input System, Unity UI | Partial | not completed yet | 9/27 |
| TC-9.2 | Integration | Pause menu exists and is navigable | SR-9.2 | Dylan | Unity Test Framework, Unity Input System, Unity UI | Yes | not completed yet | 9/27 |
| TC-9.3.1 | Unit | Verify that when playing in a sandbox environment the user can see the HUD with all the important information displayed | SR-9.3 | Dylan | Unity Test Framework, Unity UI | Partial | not completed yet | 10/4 |
| TC-9.3.2 | Unit | Verify that the HUD components stay in their respective places and are correctly interactable when needed to be | SR-9.3 | Dylan | Unity Test Framework, Unity UI | Partial | not completed yet | 10/4 |
| TC-9.3 | Integration | HUD shows when playing the game | SR-9.3 | Dylan | Unity Test Framework, Unity UI | Partial | not completed yet | 10/4 |
| TC-9 | System | Menus | PR-9 | Dylan | Unity Test Framework, Unity Input System, Unity UI, Unity SceneManager | Partial | not completed yet | 10/4 |
| TC-10.1.1 | Unit | Verify that the tutorial prompts and explains player movement (WASD) and camera look controls | SR-10.1 | Janis |  |  | not completed yet |  |
| TC-10.1.2 | Unit | Verify that the tutorial explains component interaction, placing gates, and connecting wires | SR-10.1 | Janis |  |  | not completed yet |  |
| TC-10.1.3 | Unit | Verify that the tutorial covers opening inventory, hotbar selection, and menu controls | SR-10.1 | Janis |  |  | not completed yet |  |
| TC-10.1 | Integration | Tutorial Covers all player inputs | SR-10.1 | Janis |  |  | not completed yet |  |
| TC-10.2.1 | Unit | Verify that tutorial text instructions and keybind prompts are clear and easy to follow | SR-10.2 | Janis |  |  | not completed yet |  |
| TC-10.2.2 | Unit | Verify that visual highlights or markers clearly indicate the required player actions | SR-10.2 | Janis |  |  | not completed yet |  |
| TC-10.2.3 | Unit | Verify that completing an action provides feedback and progresses the player to the next tutorial step | SR-10.2 | Janis |  |  | not completed yet |  |
| TC-10.2 | Integration | Tutorial is understandable | SR-10.2 | Janis |  |  | not completed yet |  |
| TC-10 | System | Controls Tutorial | PR-10 | Janis |  |  | not completed yet |  |
