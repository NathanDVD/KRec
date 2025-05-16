# Krec

## This app is basically a key logger that saves keyboard as well as mouse inputs (including movements).

#### It stores everything in json files to be able to "replay" what has been saved.
#### The program is a basic console app written in C# for a personnal project, created to try automate games/ actions and be able to share them to other users.

---

# How to get it

#### Download the 7zip archive from main, extract and run the executable.

## How to use?

### 1: Choose the mode you want to use (record inputs or read a file)

### 2:
| *Record inputs* | *Read a file* |
|:---|:---|
| -Choose a name for the file to save to.<br>(Will Automatically add the extension.) | -Choose a file to read<br>(No need to specify the extension) |
| -Press **_F1_** to start recording, **__F2__** to pause/stop <br> and **__F3__** to close the app.<br>-**__F2__** and **__F3__** both save the inputs, closing the console without saving <br>will result in the loss of the inputs. | -Enter your screen dimensions<br>(Used for mouse movements)<br>-Press **__F1__** to start 'playing' the file. |
---
>[!Warning]
>The screen size is not working properly for now, use 1920x1080 as a value to have something not too buggy
---
>[!NOTE]
>All the files are saved in the folder ./SavedDataFiles/
---
<br></br>
## And this is all, you can share the files with anyone and it should work, as long as the screen size is put correctly.
