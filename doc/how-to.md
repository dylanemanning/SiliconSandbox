# How To Silicon Sandbox  

## For Players:  

### How To Create A World:  

In order to interact with the core of Silicon Sandbox a user must be able to move from the launch screen to a new world in order to build and simulate circuits.  
This process begins on the main menu, which should be the screen a user is on immediately after launching the game.  
  
1. Click the “Play” button towards the center of the screen  
2. Click the “New Project” button towards the bottom of the screen  
3. Type a name you will recognize into the textbox (Which will initially say “Enter Text…”)  
4. Click the “Start” button below the textbox  
5. If you do not enter the game scene check below the text box for an error message, this normally means you have used invalid characters, too many characters, or an existing world name. Try a new name and click Start again  
  
After this the menus will disappear and you should see an empty 3D world, with a hotbar and sandy blocks leading to the horizon.  

### How To Use The Inventory:  

The inventory stores all the gates, tools, wires, and packages a user may need in designing their circuits. In order to use an item from the inventory a user must place it in their hotbar. The inventory is much larger and can only be accessed by bringing up the inventory UI. The hotbar contains 10 items and will be visible and accessible as long as the user is in an active 3D world.  
This process begins once a user is already in a 3D world  

1. Press the button bound to open inventory (Bound to E by default) to open the inventory UI  
2. Hover your mouse over a grid square in the inventory to see the name of the block at the bottom of the inventory UI  
3. Press a number key 1-0 to move the item being hovered over into that numbered hotbar slot  
4. Press esc to close the inventory UI  
5. Press a key 1-0 to select the item in that numbered hotbar slot  
6. Click the button bound to place block (right click by default) to place that item into the world

You should have seen the inventory UI open and close in this process, and now you should see whatever block you have placed in front of you.

### How To Package A Circuit:  

Note: At this time packaging is unimplemented, and will exist in an update soon.  
Packaging is an important feature in Silicon Sandbox.Packaging is the compression of multiple circuit components into one functional block. It will allow users to simplify large and complex circuits to reduce visual clutter and focus on just key aspects of any given design.  
This process begins once a user is already in a 3D world, and has placed several connected circuit components.  

1. Determine the area you would like to package  
2. Enter the inventory  
3. Add the “Packaging Wand” item to your hotbar  
4. Select the hotbar block containing the packaging wand by pressing the respective number on the keyboard  
5. Select one corner of the area to be packaged by clicking the button bound to place block (right click by default)  
6. As you move and look at other blocks you should see a white outline creating a cube originating at the corner you selected  
7. At the opposite corner of the area you’d like to package click the place block button again  
8. You should see a prompt asking you to confirm the packaging area, press yes if this is the area you’d like to package and no if you’d like to adjust.  
9. If you pressed no you should return to step 6  
10. If you pressed yes a new popup will appear, asking for information about the new package  
11. Enter the name of the package in the textbox titled name, and fill out or change any other details that don’t match the standard layout.  
12. Press “Confirm”

You should see the circuit in front of you remain, unchanged. The white cube outline should disappear. If you open the inventory you should see a placeable packaged block with the same name you just entered.  

### How To Adjust Your Keybinds:  
User adjustable controls are standard in most video games. This is to allow users to configure their game however best suits their needs, since not all players will have the same keyboard layouts or same motor associations for what keys should cause what behavior.  
This process begins on the main menu, which should be the screen a user is on immediately after launching the game.  

1. Click the “Settings” button towards the center of the screen  
2. This will bring you to the settings tab, you should be on the controls page by default  
3. Find the line relating to the keybind you would like to change by finding the row with the action you’d like to adjust on the left side  
4. Left click the button on the right side of that row (which should contain text relating to the current keybind of that action)  
5. Press the button you would like to be bound to this action  
6. The text on that button should now be replaced with the keyname of the key you just pressed  
7. Press the “Save” button on the bottom left corner of the menu  
8. Press the “Exit” button on the bottom right corner of the menu  

This process should bring a user back to the main menu screen.  
Note: This action can also be done from the 3D game scene. To do this replace all reference to the main menu with the pause menu, which can be accessed from the 3D game scene by pressing the pause keybind (defaulted to Esc)  

## For Developers:  

### How To Update the Current Version:  

As Silicon Sandbox grows and matures we want to keep users in the loop with the current gamestate. This is most simply done with the version number, which should follow classic SemVar style (vX.Y.Z). Updating the current version will update the version on the homescreen for users to see, update the title of the created build, and the version shown on the deployment platform.  
This process will take place after opening a terminal in the project directory to the codebase and the Unity Editor to the project.  

1. In the Unity Editor locate and press the “Edit” dropdown of the menu bar  
2. At the bottom of this dropdown, press the “Project Settings” button  
3. Within the Project Settings popup press the “Player” settings from the lefthand sidebar  
4. Find the textbox labeled “Version” and update it sequentially with the current version, just X.Y.Z, no text necessary  
5. Switch to the terminal  
6. Commit any remaining changes relating to the new version  
7. Run the command **git tag vX.Y.Z** where X.Y.Z are EXACTLY the same as the entry to the Unity settings  
8. Run the command **git push --tags**  

This should change the version number displayed on the main menu of the game, and once the actions.yml is run on this commit the generated build will include the version number. Finally, when deploying this build the version number should automatically process through the deployment pipeline and update the Steam store page.  

### How To Manually Run the Test Suite:  

Silicon Sandbox has an ever growing suite of manual tests, and it is inefficient to wait for a github action to check each test. All of these tests can be run in the unity editor for convenience, more immediate feedback, and more robust error messages when a test does fail  
This process will take place after opening the Unity Editor to the project.  

1. In the Unity Editor locate and press the “Window” dropdown of the menu bar  
2. At the top of the 3rd divided section in the Window menu locate the "General" menu   
3. Click the "Test Runner" option at the top of the 4th divided section    
4. This should open a popup title "Test Runner"  
5. At the bottom right of this popup is a button labelled "Run All," for general use press this button  
6. For more specific testing open the Silicon Sandbox dropdown and the Assemblt-c++-dll dropdown  
7. Select the specific tests you would like to run and press the button beside "Run All," "Run Selected"  
8. You may repeat these steps in the EditMode, PlayMode, or Player sections, which you can swap between with the buttons at the top center of the pop up (Note that by default you will be in EditMode)  

You should see the tests one by one appear with either a green checkmark or a red skip icon. Clicking any test with an icon next to it will show what failure occurred and any text printed by the test.  
