 


 

 

 

 

Software Development Plan

Silicon Sandbox

ECE 49595SDI – Open Source Software – Spring 2026

 

 

 

 

 

 

Dylan Manning, manningd@purdue.edu

Gray Dufilho, gdufilho@purdue.edu  

Janis Mikits, jmikits@purdue.edu


 

 

**​​**Table of Contents 

​ 

[**Project Overview	3**](#project-overview)

[**Scope	3**](#scope)

[**Requirements	6**](#requirements)

[Functional Requirements:	6](#functional-requirements:)

[Non-Functional Requirements:	13](#non-functional-requirements:)

[**Deliverables	17**](#deliverables)

[**Standards	17**](#standards)

​ 

​ 

**​** 

​ 

**​** 

**​** 

**​**

# Project Overview  {#project-overview}

We are creating Silicon Sandbox so that computer engineering students can easily learn digital design through an engaging hands-on game, leading to a better understanding of design material while still having fun. Silicon Sandbox is a 3D block placing sandbox game specifically designed to visualize and build digital systems from the ground up, starting with logic gates, and building all the way to fully functioning integrated circuits. A user of the game can first design simple digital components with logic gates. These logic gates can then be condensed and packaged into a singular block which the user can use to build larger components, and eventually a packaged integrated circuit. This progression gives the user an opportunity to learn various levels of design abstraction in a hands-on format that is easier to understand than traditional schematics and RTL diagrams.

# Scope {#scope}

* **In Scope**:   
  * S1: Circuit Building  
    * Users must be able to connect blocks, wires, inputs, and power supplies to create a full circuit of their design.   
  * S2: Logic Simulation and Visualization  
    * Once a system is built users must be able to simulate the system running, see the direction of logical flow, and test the inputs and outputs  
  * S3: 3D Building and Interaction  
    * Users must be able to make use of a 3 dimensional space where they can place, move, delete, and change objects around them.  
  * S4: Packaging  
    * Once a circuit is created, the user must be able to package it into a single block that is able to be placed in the sandbox environment and used as a functional component. This is useful in reducing the size and complexity of large systems.  
  * S5: Inventory  
    * Users must be able to hold and use unlimited instances of logic gates and packaged modules. These blocks will be selected from an inventory pop-up window or static hot bar.  
  * S6: Controls Tutorial  
    * Users must have the option for a brief tutorial of how to control the game. This includes movement, placing blocks, connecting wires, and running simulations.  
  * S7: Standard Computer Game Controls  
    * Users need to be able to interact with all aspects of the game using a keyboard and mouse. This includes anything needed to interact with a standard game.  
* **Under Evaluation**:   
  * E1: Sharing builds  
    * Users may want to be able to send their builds to other users, we may or may not provide direct support to do this.  
  * E2: Basic logical tutorial  
    * Some users may benefit from a guide to make a simple initial system, like a decoder. Depending on time allowance we may include a walkthrough of building such a system.   
* E3: Achievements and design objectives  
  * If time permits at the end of the semester, we will include a basic project plan that a user can follow to learn all of the topics and systems covered in ECE 337 and ECE 437 at Purdue University. This plan will be an outline of topics, not an in-depth tutorial. This project plan will also be the extent of our “gamified mechanics” because we believe any further “gamification” would detract from the sandbox nature of the game.  
* E4: Digital design explanations  
  * If time permits after including E3, we will provide resources on the digital design topics covered in E3. These resources will explain the topics in detail so that the user better understands what they are building. This feature would essentially replicate ECE 337 and ECE 437 course material. Due to our assumption of user knowledge outlined in A1, this feature should not be necessary and will be our lowest priority to implement.  
* **Out of Scope**:   
  * O1: Multiplayer  
    * We will not include compatibility for multiple players to work in tandem on the same project or servers.   
  * ~~O2: Instruction of digital systems~~  
    * ~~This game is not meant to provide instruction. The game will not contain instructions on how to build large systems or why the systems work the way they do.~~  
  * ~~O3: Gamified mechanics~~  
    * ~~While functioning as a game, this project is not meant to act like a typical story-mode game. Users will not be provided with objectives, points, survival mechanics or anything similar.~~  
* O2: Survival mode  
  * This project is purely meant for design and will therefore not contain any survival mode mechanics such as health, food, enemies, or limited resources.  
  * O3: Translation out of English  
    * This project will be written entirely in English and will not be translated into any other languages.  
  * O4: Console support  
    * This project is meant to run on a computer and will not be created with multi-platform compatibility.   
* **Assumptions**:   
  * A1: Users ~~already understand logical flow~~ Are Familiar With Digital Design  
    * ~~Similar to what is mentioned in O2~~Unless E3 or E4 become in-scope, this project is not meant to instruct users. This game is meant to be used as a tool to understand engineering course material. We assume anyone using our system already understands the mechanics of logic gates, wires, and signal propagation. This game is meant to be an educational alternative to learning digital design with system Verilog. This game is not meant to be a development tool for professionals. ~~at least the basics of connecting systems how how bits and how signals flow through a system.~~  
  * A2: Users want a place to experiment and test design  
    * We are assuming this is a product that users would find desirable for learning digital design and practicing engineering course material. ~~want, and a valid way to test designs and systems before full construction.~~  
  * A3: A 3D environment is more immersive and easier to understand than 2D  
    * We ~~have assumed~~ are assuming that building in~~to~~ a 3D environment improves ~~provides benefits to~~ a user's understanding and creativity.   
  * A4: Users have access to a computer and steam  
    * We ~~have assumed~~ are assuming that ~~the majority of~~ users interested in ~~this project~~ Silicon Sandbox and are able to run the game via steam on a personal computer. ~~have access to a personal computer and steam to access and run this game.~~  
* **Constraints**:   
  * C1: Limited development time  
    * We are developing the brunt of this game in one semester, and all of it within just two semesters. This creates a constraint on how much we can feasibly get done. Many of the projects inspiring ours had much longer, and continuous, development cycles.   
  * C2: Limited initial skill of developers  
    * No one on the development team is familiar with building games, especially not 3D ones. We did not start the project with many of the necessary skills, so much of our development timeline will include learning as we go.  
  * C3: Accessibility Constraints  
    * We must follow accessibility guidelines, in particular this means multiple ways to differentiate items and lack of flashing lights. 

# Requirements {#requirements}

### Functional Requirements: {#functional-requirements:}

1. FR-1: Project Selection  
   1. **Statement**: When a user clicks on an existing project file in the project selection menu under standard circumstances, the game client shall open the project ~~The system shall allow a user to open an existing project from a selection menu~~ within 5 seconds ~~in standard circumstances~~.  
   2. **Rationale**: Users shouldn’t have to recreate a completed project every time they want to return to it.   
   3. **Test Method**: Create 5 saved projects, close the application, reopen it, and attempt to open each one 10 times. If 9/10 times each project opens within 5 seconds the test is a success.   
   4. **Supporting Context**: A project is a saved game environment containing all placed modules, wires, and other components.   
   5. **Tracing Information**: S3, S7  
   6. **Priority**: Must Have  
2. FR-2: Game client  
   1. **Statement**: When a user launches the game application, the application shall provide a functional game client that allows a first-person avatar to navigate, manage an inventory, and interact with electronic components.   
   2. **Rationale**: A game client is necessary for the player to interact with the system as a whole.  
   3. **Test Method**: Launch the program, move the player character forward, backward, left, right, up, and down. Open and close the environment. Place a component. If all actions complete successfully 5 times then the test is a success.  
   4. **Supporting Context**: A game client is the executable application running on the user’s computer rendering the environment and interpreting player input.  
   5. **Tracing Information**: S2, S3, S7  
   6. **Priority**: Must Have  
3. FR-3: Viewport  
   1. **Statement**: When 1,000 logic gates or less are placed in a project, the ~~system~~ game client shall render the game environment in a viewport that updates at a minimum of 30 fps ~~during normal operation~~.  
   2. **Rationale**: A functional viewport ensures the user is able to interact with and view the environment and game components.   
   3. **Test Method**: Launch the system and create a circuit with 1,000 logic gates ~~50 components~~. Run the system and monitor the output for 5 minutes. If the average fps remains at ~~30~~60fps or higher, the test passes.  
   4. **Supporting Context**: The viewport is the viewable area of the application window containing all the information from the project, as opposed to the information relevant to the OS.  
   5. **Tracing Information**: S2, S3, S7  
   6. **Priority**: Must Have  
4. FR-4: Application Window  
   1. **Statement**: When the game application is run, the application shall display the game client within a resizable application window capable of minimizing, maximizing, resizing, and closing using standard system controls.  
   2. **Rationale**: Users must be able to manage the application along with other programs and interact with their operating system normally.  
   3. **Test Method**: Launch the application. Resize, minimize, maximize, and close the window. Repeat 5 times. If the system is able to handle this without crashing or freezing (or handling an unexpected exit ~~end gracefully~~ in the case of closing) the test passes.  
   4. **Supporting Context**: The application window is the operating system level container that holds the viewport and user interface elements. ~~S7~~  
   5. **Tracing Information**: S7 ~~The application window is the operating system level container that holds the viewport and user interface elements.~~   
   6. **Priority**: Should Have  
5. FR-5: Packaging  
   1. **Statement**: When a user selects a bounding box for packaging, the package compiler shall ~~allow users to~~ package ~~a~~ that group of components into a single, reusable block with defined input and output pins that retains the logic of the original group of components.  
   2. **Rationale**: Packaging allows users to reuse subsystems without having to rebuild them, and greatly decreases the size of the final top level system.   
   3. **Test Method**: Create a  circuit with at least 5 logic gates, and multiple inputs and outputs. Package it. Attempt to place the package in a new circuit and verify the full truth table is preserved.  
   4. **Supporting Context**: A package refers to the group of components simplified to one block.  
   5. **Tracing Information**: S4  
   6. **Priority**: Must Have  
6. FR-6: View K-map representation of a package  
   1. **Statement**: When a user selects a package, The ~~system~~ game client shall ~~allow a user to select a package and~~ open a view of the K-map representation of said package.  
   2. **Rationale**: A K-map representation is the simplest way to compare the inputs and outputs of large systems, allowing a user to verify that their system behaves as intended without direct testing.  
   3. **Test Method**: Create a package with multiple inputs and outputs. Select the package and open the K-map view and verify it matches the expected behavior of the circuit. Repeat 3 times with different circuits. If all three K-maps match the test passes.  
   4. **Supporting Context**: A K-map (Karnaugh-map) is a compressed visual representation of a truth table laid out in a grid. It is often used to represent multiple inputs and potential outputs.   
   5. **Tracing Information**: S2, S4  
   6. **Priority**: Should Have  
7. FR-7: Expand package back into components  
   1. **Statement**: When a user selects a package and selects the expand package  button, the package compiler shall ~~allow users to select a package and view~~ create a view of the original circuit the package was built from.  
   2. **Rationale**: By being able to view the interior of a package users are able to reinforce their understanding of how different packages interact and why a package behaves the way it does. This also allows the editing of existing packages instead of having to create a new one from scratch in the case of an error.  
   3. **Test Method**: Create a circuit and package it. Expand the package and verify the components remain the same. Swap out one component for another ~~making a notable change~~. Repeat 5 times, verifying each time the change is saved in the package. If the package shows the expected circuit every time, the test passes.  
   4. **Supporting Context**: See above definition for a package.   
   5. **Tracing Information**: S3, S4  
   6. **Priority**: Must Have  
8. FR-8: UI  
   1. **Statement**: The system shall provide a user interface that includes a main menu, settings, hotbar, and inventory interface.   
   2. **Rationale**: Users need a clear interface to access all system functions and select options or components.   
   3. **Test Method**: Launch the application. Traverse the start menu, open the settings menu and traverse all pages. Launch a project and traverse all cells in the hotbar. Open the full inventory and interact with multiple cells and inventory options. If all traversals are possible the test passes.  
   4. **Supporting Context**: The hotbar is a toolbar of quick access items from the inventory, the items in the hotbar are available during normal gameplay.  
   5. **Tracing Information**: S3, S5, S7   
   6. **Priority**: Must Have  
9. FR-9: Inventory  
   1. **Statement**: The system shall give every player an inventory to store and select base logic gates and custom packages.  
   2. **Rationale**: Users will need to be able to access a variety of components and packages at all times, and the inventory will give them a place to store these components when not immediately in use.  
   3. **Test Method**: Open the inventory and ensure all base gates are selectable. Package a circuit and add it to the inventory. Make sure it is selectable. Place at least 5 base components and 5 packaged components from the inventory into the world. If no errors are encountered the test passes.   
   4. **Supporting Context**: An inventory is a menu containing   
   5. **Tracing Information**: S5  
   6. **Priority**: Must Have  
10. FR-10: Textured Environment  
    1. **Statement**: The system shall have a textured environment in the playable area.  
    2. **Rationale**: The user will be more comfortable with a familiar looking area to interact and design in.   
    3. **Test Method**: Load into a world/save file and ensure that the environment is textured and it is apparent what the design is. It must not be blank or monochrome and must not break the illusion of the game. If it is clearly textured then the test passes.   
    4. **Supporting Context**: A texture is an image overlaid on top of models and elements of a game environment. An example is a texture of grass overlaid on the floor to give the illusion of a grassy plain.   
    5. **Tracing Information**: S3, S7  
    6. **Priority**: Should Have  
11. FR-11: Bounding Walls   
    1. **Statement**: The system shall have walls bounding all directions of the user playable area, and said walls shall be textured so that they give the illusion of not being present.   
    2. **Rationale**: The system cannot have an infinite world and as such needs to have bounding walls to keep the player inside the game area.   
    3. **Test Method**: Load a new world / save file and move the player character forward until hitting a wall. Test in all directions and ensure the walls are textured. If the player cannot escape and the bounding walls are textured then the test passes.   
    4. **Supporting Context**: The playable area is the area that the user will be during the game and interact with object and game systems.   
    5. **Tracing Information**: S3, S7  
    6. **Priority**: Should Have  
12. FR-12: Player Character  
    1. **Statement**: The system shall have a moveable character that can interact in 3D space, called a Player Character.   
    2. **Rationale**: The user needs a medium with which they will use to interact with the simulated world. This in game avatar will be the player character.   
    3. **Test Method**: Load into a new world / save file and navigate in the environment while interacting with any elements. If no errors occur then the test passes.   
    4. **Supporting Context**: See above definition for player character.   
    5. **Tracing Information**: S3, S7  
    6. **Priority**: Must Have  
13. FR-13: Block Placing and Breaking  
    1. **Statement**: The system shall have set processes to ensure that the user can accurately place blocks and then also delete those blocks.   
    2. **Rationale**: The user cannot easily interact with the environment if they cannot do so with any accuracy. The user must be able to place blocks accurately for testing and designing purposes. The user must also be able to break blocks in case they want to move components or simply misplace a component.  
    3. **Test Method**: Deliberately place and delete blocks on a specified point ~~A to another specified point B~~. If no errors occur or are encountered, then the test passes.   
    4. **Supporting Context**: Blocks could be a variety of different in game elements, such as a packaged module, transistor, or another interactable in game element.   
    5. **Tracing Information**: S1, S2, S3  
    6. **Priority**: Must Have  
14. FR-14: Block Models  
    1. **Statement**: The system shall include models for each block in the game and each block or packaged design so that they are visual and distinct from each other.    
    2. **Rationale**: The user will need to determine which blocks are which and be able to differentiate between blocks. They also need to be able to visually see the block and where they are located in the playable area.   
    3. **Test Method**: Place a variety of different blocks in the playable area. Have the user then point out where the blocks are located and what each block is. If the user can accurately perform this, then the test passes.   
    4. **Supporting Context**: Models are the visual components of in game elements and allow the user to visually see representations of the game elements.   
    5. **Tracing Information**: S2, S3  
    6. **Priority**: Must Have  
15. FR-15: Complete Logic Gates  
    1. **Statement**: The system shall have all of the logic components necessary for a complete game, therefore it must have all logic gates available / createable easily.   
    2. **Rationale**: Without access to all the logic gates, it would be significantly harder to make any advanced or more advanced digital systems.   
    3. **Test Method**: Load a new world / save file and navigate through the menus to ensure that there are the elements for all the logic gates. Ensure that each logic gate can be placed in the playable area and that each logic gate functions correctly.   
    4. **Supporting Context**: A logic gate is a fundamental element of digital systems and some examples of some are AND and OR gates.  
    5. **Tracing Information**: S1, S2  
    6. **Priority**: Should Have  
16. FR-16: In-Game Clock  
    1. **Statement**: The system should have a functional in-game clock whose frequency can be modified to the user’s satisfaction and need.   
    2. **Rationale**: For the user to create many digital logic devices and systems a clock is necessary for use and timing. Without a clock element in the game, many systems will get exponentially harder to design and implement.  
    3. **Test Method**: Place the in game clock element and ensure that it properly outputs a signal at the correct frequency. Test then that the clock can be modified to any frequency and that the functionality stays correct. If no errors occur, the test passes.   
    4. **Supporting Context**: A clock is a system that pulses a signal at set intervals based on the frequency that it is set to. It is used in many applications and systems in digital logic design.   
    5. **Tracing Information**: S1, S2  
    6. **Priority**: Must Have  
17. FR-17: Accurate Logic   
    1. **Statement**: When a user simulates a circuit, the ~~system~~ game client shall have accurate logic for ~~the~~ those logic gates ~~in the game~~. The output of each logic gate shall be correct and mirror real life and any system using the logic gates shall also behave predictably with correct outputs.  
    2. **Rationale**: The game needs to be as close to real life in terms of simulating inputs and outputs of all the logic systems. Without being accurate, many systems will not work and the game as a whole will not be a correct simulation.   
    3. **Test Method**: Place all logic gates and check every possible input and output combination to ensure accuracy. Once this is done and is correct, create a ~~slightly more complex~~ system that utilizes 50% more ~~the~~ logic gates and ensure the correctness of that system as well. If the inputs and outputs mirror the expected values then the test passes.   
    4. **Supporting Context**: See above definition of logic gates (FR-16).   
    5. **Tracing Information**: S2  
    6. **Priority**: Must Have  
18. FR-18: Linking Tool  
    1. **Statement**: The system must implement a real-time linking tool that automatically generates a logical netlist between components, according to the wires connecting the components.  
    2. **Rationale**: To ensure the inputs and outputs of any system stay as accurate as possible the system must keep track of how each element is connected to any other elements.   
    3. **Test Method**: Create a simple and a complicated system that each connects different elements to each other and ensure that the netlist created has accurately kept track of each connection. If the netlist is correct for both systems and no errors come up, then the test passes.    
    4. **Supporting Context**: A netlist is a text-based, structured description of an electronic circuit's components.   
    5. **Tracing Information**: S1, S2  
    6. **Priority**: Must Have

19. FR-19: Source Blocks  
    1. **Statement**: The system shall implement both voltage and current source blocks that initiate the simulation process by powering circuits with a user-chosen number of amperes or volts.  
    2. **Rationale**: Circuits need power to function.   
    3. **Test Method**: Create a simple and a complicated system that each connects different elements to each other and then complete that circuit with a source block. If the correct output is observed, then the test passes.    
    4. **Supporting Context**: Whenever the term simulation is used in this document, it is referring to the use of source blocks to power the circuit, which in turn initiates the simulation of that circuit.   
    5. **Tracing Information**: S1, S2  
    6. **Priority**: Must Have

20. FR-20: Wire Mechanics  
    1. **Statement**: The system shall include placeable wire blocks that can be connected to components and propagate signals through them.  
    2. **Rationale**: Signals need a medium to pass through in order to propagate through logic gates. Without wires to connect ports on various gates, those logic gates are useless.  
    3. **Test Method**: Connect two logic gate ports with a wire. Then power the system. If the signal correctly propagates through the logic gates, then the test passes.  
    4. **Supporting Context**: Wires will attach to the ports on the sides of logic gates. They will change color when signal is flowing through them so that the user can see what is powered in the circuit.   
    5. **Tracing Information**: S1, S2  
    6. **Priority**: Must Have

### Non-Functional Requirements: {#non-functional-requirements:}

1. NFR-1: Simulation FPS  
   1. **Statement**: When simulating 1,000 logic gates or less, the game must maintain at least 60 frames per second.  
   2. **Rationale**: A minimum rate of 60 frames per second during in game simulations allows for enjoyable gameplay without lag.  
   3. **Test Method**: One round of testing will be done with each logic gate, as well as one round of testing with a combination of equal amounts of each logic gate. Each test will consist of connecting 1,000 logic gates in random configurations and then simulating their execution. Record the frame rate for each test and observe if it is at least 60 frames per second for the entire simulation.   
   4. **Supporting Context**: Assume there are no other components or gates present in the game file and that the logic components are all packaged into one component to mimic realistic designs.  
   5. **Tracing Information**: S2  
   6. **Priority**: Should Have  
2. NFR-2: Dynamic Link Library  
   1. **Statement**: The software architecture shall utilize a native C++ Dynamic Link Library (DLL) as the primary simulation engine to decouple logic processing from Unity rendering.  
   2. **Rationale**: Using Unity for both simulation and rendering would be incredibly slow because Unity is not optimized for complex data simulation. Using a DLL to perform calculations will significantly improve game performance.  
   3. **Test Method**: Call a function in C\# inside of Unity that modifies AND-GATE block placement within a world file and returns the data using C++. Verify that the data was passed through the DLL and correctly placed the AND-GATES in the same configuration.  
   4. **Supporting Context**: The DLL is a file that contains shared code, data, and resources and allows for game logic and computation to be completed via C++, instead of C\# inside of Unity. Such computation in C++ is much faster than in C\# inside of Unity.  
   5. **Tracing Information**: S2, S3, S4  
   6. **Priority**: Should Have  
3. NFR-3: Feature Scalability  
   1. **Statement**: The software architecture shall allow the addition of new base blocks other than logic gates in the user’s inventory.  
   2. **Rationale**: This flexibility allows for updates in the future to include sensors, displays, or other components. These updates would allow for the user to explore topics such as Input / Output interfaces.  
   3. **Test Method**: Create a dummy block that has no functionality. Include the dummy block as a new base component that can be retrieved from the user’s inventory. Observe if the block can be retrieved from the user’s inventory and placed on the sandbox ground.  
   4. **Supporting Context**: In the initial version of the game, the only base components available to the player are the following logic gates: AND, NAND, OR, NOR, XOR, XNOR, BUFFER, and NOT. All other component blocks must be created by the player through packaging logic gates or other self made component blocks.  
   5. **Tracing Information**: S1, S5  
   6. **Priority**: Could Have  
4. NFR-4: Default Keybinds  
   1. **Statement**: The software architecture shall implement the same default keybinds as Minecraft.  
   2. **Rationale**: Most users will already be familiar with Minecraft keybinds, so including them as default keybinds will ensure users are able to more easily play the game without confusion.  
   3. **Test Method**: Press each button shown in the keybind mapping in settings. Observe if the appropriate action is completed.  
   4. **Supporting Context**: The keybinds referenced from Minecraft will be the keybinds for the PC Java version of the game.  
   5. **Tracing Information**: S6, S7  
   6. **Priority**: Should Have  
5. NFR-5: Packaging Bounding Box  
   1. **Statement**: When specifying in-game coordinates to package a component, a visible bounding box shall appear around the specified coordinates and will update within 0.1 seconds after the user changes the in-game coordinate bounds.  
   2. **Rationale**: Visualizing what logic gates and subcomponents will be packaged into a larger component makes laying out a circuit easier for the user, because they can avoid mistakenly adding too few or too many gates to a component package.  
   3. **Test Method**: Complete 10 rounds of testing. During each round, increment the bounding box coordinates by 10 blocks in each of the X / Y / Z directions, one increment at a time. Increment 10 times, ending with a bounding box of (100, 100, 100\) blocks. Include a performance log in the game code that records the exact time that coordinates were inputted, and the exact time that the viewport was updated. Construct a function that computes the difference in time between the input time and update time. If any difference is above 0.1 seconds, record what the coordinate change was, what the input time was, and how long it took for the viewport to update.  
   4. **Supporting Context**: The 3D sandbox world will be made of cubic blocks. Each block represents one unit in a coordinate system that starts at (0, 0, 0\) from spawn. The user will use a packaging interface described in NFR-6 to write the dimensions and coordinates of the bounding box that will be compacted into a singular component block via the packaging system.  
   5. **Tracing Information**: S4  
   6. **Priority**: Could Have  
6. NFR-6: Packaging Interface  
   1. **Statement**: The packaging interface shall be designed so that a first-time user can create a package in 10 minutes or less without the help of external documentation.  
   2. **Rationale**: The packaging system is a main feature of the game, so it is important that it only takes 10 minutes to learn how to use, especially for new users. Otherwise, the users will be limited to using logic gates alone.  
   3. **Test Method**: Have 5 people who have never played the game before attempt to package an already built design of logic gates. Time how long it takes them to create a package. Observe if any of the new players take over 10 minutes to create the package.  
   4. **Supporting Context**: The new users will be given a tutorial on how to place blocks, interact with the inventory, and move around in the viewport, prior to completing the packaging test. This format of testing will accurately mimic actual gameplay because new users will first create a design before learning how to package it.  
   5. **Tracing Information**: S4  
   6. **Priority**: Should Have  
7. NFR-7: Autosave  
   1. **Statement**: The backup manager shall automatically save the game’s world file every five minutes to a local backup folder and delete autosaves from greater than 20 minutes ago as long as there are four other backup files in the folder.  
   2. **Rationale**: Game crashes occur for a variety of reasons so it is convenient to have an automatic backup folder to avoid loss of work.  
   3. **Test Method**: Run the following test 5 times. Create a new world file. Every minute, add another AND GATE to the world. After 30 minutes, close the game. Open the backup folder and observe if there are four backup files, one from each of the following times: 5 minutes ago, 10 minutes ago, 15 minutes ago, 20 minutes ago. Open each of the files and observe if they contain the correct number of AND GATES that would have been placed at that time.  
   4. **Supporting Context**: When a player exits the game, that game file will be saved to a regular file to be retrieved in-game. The backup folder is separate from the file where all up to date game files are stored.  
   5. **Tracing Information**: S2  
   6. **Priority**: Could Have  
8. NFR-8: Timing Constraints  
   1. **Statement**: All logic gates shall adhere to IEEE standard timing constraints.  
   2. **Rationale**: For large designs, timing constraints have a significant impact on design layout. This game is used as an educational tool so it is important that in-game components are as similar to real world components as possible.  
   3. **Test Method**: Build a line of 100 AND GATES in series. Simulate the design and observe the performance log to see when data arrived at each of the AND GATES inputs and exited each of the outputs. Observe if all of the times are at most the required times by IEEE timing constraints.  
   4. **Supporting Context**: Timing constraints will include propagation times and setup/hold times.   
   5. **Tracing Information**: S2  
   6. **Priority**: Could Have  
9. NFR-9: Tutorial and Documentation  
   1. **Statement**: When a new user plays the game for the first time, they shall be able to learn every aspect of gameplay through in-game documentation and tutorials.  
   2. **Rationale**: Since this is a new game with no user base or online tutorials, it is essential that new users are able to learn how to play the game, otherwise, they will not know how to use every feature or even know how to start a design.  
   3. **Test Method**: Have 5 people who have never played the game before attempt to play the game for an hour. Allow them to look through the documentation and take the tutorial on their own, without assistance. After an hour, give them a list of tasks to accomplish in-game that include all of the features covered in the tutorial and documentation. Observe if they are able to accomplish each task by using the documentation and tutorial, without external assistance.  
   4. **Supporting Context**: It is not assumed that each new user will be a subject matter expert in the game within one hour. It is assumed that any user is capable of finding an answer to how to use any feature of the game on their own using the documentation and tutorial.  
   5. **Tracing Information**: S6  
   6. **Priority**: Must Have

10. NFR-10: Steam Page  
    1. **Statement**: The completed game shall be available for download on Steam.  
    2. **Rationale**: Users need a convenient location to download our game. Steam is the most trusted platform in the industry for purchasing games, therefore we will publish our game on Steam.  
    3. **Test Method**: Have 5 people attempt to download the game from the steam store. If they are all able to download the game and add it to their library, then this test is successful.  
    4. **Supporting Context**: New users need a platform to download and run the game from.  
    5. **Tracing Information**: A4  
    6. **Priority**: Must Have

# Deliverables  {#deliverables}

* **Deliverable Description**: A 3D block placing sandbox game designed for building and simulating digital circuits. The game provides a first-person environment where users can place logic gates, connect elements with wires, and utilize a packaging system to condense complex systems into compact usable blocks. It includes a complete logic simulation engine to ensure accurate logic behavior, an inventory system for management, and a complete UI for navigating the settings and system.   
  * **Relevant Requirements**: FR-2: Game Client, FR-12: Player Character, FR-13: Block Placing, FR-17: Accurate Logic, FR-18: Linking Tool, FR-5: Packaging, FR-9: Inventory, NFR-1: Simulation FPS, NFR-7: Autosave  
* **Deliverable Description**: A public facing Steam page where users can download, install, and launch the game. It will include the game executable, screenshots and descriptions of the game, system requirements, and a changelog for future updates.   
  * **Relevant Requirements**: ~~FR-2: Game client, FR-3: Viewport, FR-8: UI, NFR-3: Scalability~~ NFR-10: Steam Page

* **Deliverable Description:** An in-game documentation package and in-game tutorial for end-users. End-user documentation will include detailed information and instructions about how every block functions and how the player can interact with every feature in the game. Each piece of documentation will be searchable through a keyword search bar. The tutorial is separate from the documentation and will lead the player through how to move, place blocks, connect wires, and package logic gates into components.  
  * **Relevant Requirements:** NFR-9: Tutorial and Documentation

# Standards {#standards}

1. ISO 25010 \- Software Quality Model ([https://iso25000.com/index.php/en/iso-25000-standards/iso-25010](https://iso25000.com/index.php/en/iso-25000-standards/iso-25010))  
   1. How it’s applicable: ISO 25010 defines software quality characteristics. These characteristics are ways to measure and ensure the fulfilment of stakeholder needs. As a software team that needs to ensure fulfilment of stakeholder needs, this standard is relevant to us.  
   2. How it impacts us: We will keep these 9 quality characteristics in mind as we develop this project. It will ensure we keep a level of quality by focusing our efforts on areas such as stability, efficiency, and flexibility.  
   3. Plan for compliance: Following the tests we’ve established on the above requirements and making sure these requirements match up to the software quality characteristics we will ensure we are keeping in line with all 9 qualities to the best of our ability.  
2. IEEE 1076 \- Standard for VHDL ([https://standards.ieee.org/ieee/1076/5179/](https://standards.ieee.org/ieee/1076/5179/))  
   1. How it’s applicable: IEEE 1076 is the standard for VHSIC Hardware Description Language which describes how logic signals propagate through real-world hardware systems. As a software team that is creating a game to simulate real-world hardware systems, this standard is relevant to us.   
   2. How it impacts us: This standard informs NFR-8 and will keep our system consistent with real-world hardware standards. This will ensure that our timing and logic accuracy are consistent and correct.  
   3. Plan for compliance: We will verify compliance to this standard by testing the output of complex systems against VHDL simulation results. The accuracy of our system should match the output of the VHDL simulation to ensure compliance.   
3. IEEE 1012 \- System, Software, and Hardware Verification and Validation ([https://standards.ieee.org/ieee/1012/5609/](https://standards.ieee.org/ieee/1012/5609/))  
   1. How it’s applicable: Our system relies on simulation of digital logic, so we need to ensure that our simulations are correct.  IEEE 1012 provides a framework for verifying this.  
   2. How it impacts us: Each requirement in this standard comes with a defined test method so any system can be verified. These methods will inform our testing processes for the logical systems of the project.  
   3. Plan for compliance: Whenever we reach a milestone of development after implementing logical simulation we will run verification tests according to the standard. We can do this by simulating logical circuits we know the inputs, outputs, truth table, and k-map for. 

# Development Methodology

Our team chose the Scrum methodology for organizing our schedule and Gantt chart due to the familiarity with it. The Scrum method allows for constant meetings, iteration changes, and short sprints to work heavily on certain sections. This closely follows what we have done in design classes before and as such we decided that this was the best method for us. The Scrum method will also allow for us to constantly be testing our design and make any necessary changes as we go along throughout the semester and continue to design our product. 

# Verification & Validation Plan

[https://github.com/dylanemanning/SiliconSandbox/blob/dd4cc556df2f7802f794e548e0cfc6ddcc14d5ad/doc/Verification%20%26%20Validation-1.pdf](https://github.com/dylanemanning/SiliconSandbox/blob/dd4cc556df2f7802f794e548e0cfc6ddcc14d5ad/doc/Verification%20%26%20Validation-1.pdf)

# Gantt Chart

[https://docs.google.com/spreadsheets/d/1LTujZ3HUafClo0C6aV3tlMx9P7vmrOKKFzEQK-ntbms/edit?usp=sharing](https://docs.google.com/spreadsheets/d/1LTujZ3HUafClo0C6aV3tlMx9P7vmrOKKFzEQK-ntbms/edit?usp=sharing)