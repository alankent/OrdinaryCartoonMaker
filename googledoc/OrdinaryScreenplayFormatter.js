/*
 * Copyright 2022 Alan Kent
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

/*

This extension applies styling to a Google Document following the rules below.
It is intended for formatting a script, in a similar way to how movie scripts
are laid out. Google Docs does not have user-defined paragraph styles, so this
extension uses special characters in the text to work out what indentation etc
to apply to each paragraph.

See https://workspace.google.com/marketplace/app/fountainize/82574770793 if you
want a more traditional formatter for scripts.


Formatting rules.

Paragraph styles:
- blank lines may be dropped (or inserted) by the formatter as it feels best.
- "EXT. Corner shop - DAY" is converted to a h1 (external location + time of day)
- "INT. Kitchen - EVENING" is also converted to a h1 (internal location + time of day)
- "MUSIC: text" - bold the paragraph
- "# heading" to "##### heading" - converted to a h2 to h6 allow outlining mixed into the script
- "{directive...}" - reduced point size, grey
- "-CHARACTER-" - for character names (indented 2")
- "(parenthetical)" - parenthetical instructionsm, such as emotions, for when reading dialog (indented 1.5")
- "dialog text" - dialog after a character name before a section divider (indented 1")
- "all other text" - actions, camera directions, etc (no indent)
- ">CENTER<" - center the text (e.g. ""> THE END <"")
- "INSTRUCTION:" - right justify the instructions (e.g. "FADE TO:")

Everything before the first EXT. or INT. is never touched in a document as it is assumed
to be the title page and introductory text. So you MUST have a paragraph starting with
"INT." or "EXT." for this extension to do anything.


Character styles:
- Text inside square brackets ("[" to "]") is bolded. 


How I use it:
- Level 1 headings are left for the script writer (e.g. for "Notes", "Script", and other top level sections)
- When writing a script, I start with an outline of headings ("#" to "#####" - h2 to h5)
- I use a table of contents extension to provide an episode outline
- As I flesh out sections, I leave the headings in place (so I can still see the outline structure later)
- I am writing comics, not movies, so I adapted the screenwriting style a little
- I use [N-NN-NNN] for episode number, location number, and shot numbers.
- I increment the location and shot numbers by 10 to make it easier to insert new frames later between existing numbers.
- Because the text in [..] is bolded, they act like sub-headings (but )
- All dialog I do upper case because most comics use upper case.
- For comics, the word "I" has horizontal bars at the top and bottom, but otherwise it should not. 
  (That is, the letter captial I is formatted differently based on usage).
  So I use "|" (vertical bar) for the word "I" which is what the comic font I use does.
- For comics, "{" and "}" are shown as crows feet, allowing simple inline "{GROAN}" for emotions.


Example:


                MY TITLE DOCUMENT
                    Episode 1

Thank you for reading this script. Please send any feedback to me! Let's get into it!

# Introduce Sam

EXT. OUTSIDE HOME - MIDAY

[1-10-010] Establishing shot, pan from wide shot to focus on Sam's face

Sam is running down the street.

[1-10-020] Mid shot, frontal, tracking Sam as he runs

Add a bloom effect to Sam's hair as a nice introductory effect.

                -SAM-
            (panting)
        | AM SO LATE! | AM IN BIG TROUBLE
        THIS TIME FOR SURE!

# Introduce Mrs B

[1-10-030] Side shot showing Sam running past Mrs B

                -MRS B-
        OH, SAM! GREAT TIMING! CAN YOU GIVE ME
        A HAND PLEASE?

[1-10-040] Sam brushes her off

Shocked expression on Mrs B's face.

                -SAM-
        FORGET IT, | AM LATE FOR SCHOOL!

EXT. OUTSIDE FRONT OF SCHOOL - EARLY MORNING

[1-20-010] Sam is running up to front of school

                -TEACHER-
        LATE AGAIN SAM? DETENTION FOR YOU!

                -SAM-
        {GROAN!}

*/


// ================================ IMPLEMENTATION CODE ===================================


// Register the add-on menu item to run this reformatter
//
function onOpen(e) {
  DocumentApp.getUi().createAddonMenu()
      .addItem('Update Formatting', 'reformatDocument')
      .addToUi();
}


// On first installation, do the same registration as opening a doc.
//
function onInstall(e) {
  onOpen(e);
}


// Width of a character in points for 12 point Courier New font.
const CHAR_WIDTH = 7;

const POINTS_PER_INCH = 72;

const COLOR_DEFAULT = "#000000";
const COLOR_SUBHEADING = "#ff00ff";
const COLOR_DIRECTIVE = "#9fc5e8";
const COLOR_DIALOG = "#38761d";


// Paragraph types.
//
const ParaTypes = {
  BLANK: {
    name: "blank",
    addBlankLine: false,
  },
  H2: {
    name: "h2", 
    styles: {
      heading: DocumentApp.ParagraphHeading.HEADING2,
    },
    addBlankLine: true,
    exitDialog: true,
  },
  H3: {
    name: "h3",
    styles: {
      heading: DocumentApp.ParagraphHeading.HEADING3,
    },
    addBlankLine: true,
    exitDialog: true,
  },
  H4: {
    name: "h4",
    styles: {
      heading: DocumentApp.ParagraphHeading.HEADING4,
    },
    addBlankLine: true,
    exitDialog: true,
  },
  H5: {
    name: "h5",
    styles: {
      heading: DocumentApp.ParagraphHeading.HEADING5,
    },
    addBlankLine: true,
    exitDialog: true,
  },
  H6: {
    name: "h6",
    styles: {
      heading: DocumentApp.ParagraphHeading.HEADING6,
    },
    addBlankLine: true,
    exitDialog: true,
  },
  LOCATION: {
    name: "location",
    styles: {
      heading: DocumentApp.ParagraphHeading.NORMAL,
      leftIndent: 0,
      rightIndent: 0,
      bold: true,
      color: COLOR_DEFAULT,
    },
    addBlankLine: true,
    exitDialog: true,
  },
  PARENTHETICAL: {
    name: "paren",
    styles: {
      heading: DocumentApp.ParagraphHeading.NORMAL,
      leftIndent: 2 * POINTS_PER_INCH,
      rightIndent: 1 * POINTS_PER_INCH,
      color: COLOR_DIALOG,
    },
    addBlankLine: false,
    exitDialog: false,
  },
  CHARACTER: {
    name: "char",
    styles: {
      heading: DocumentApp.ParagraphHeading.NORMAL,
      leftIndent: 2.5 * POINTS_PER_INCH,
      rightIndent: 1.5 * POINTS_PER_INCH,
      color: COLOR_DIALOG,
    },
    addBlankLine: false,
    exitDialog: false,
  },
  CENTER: {
    name: "center",
    styles: {
      heading: DocumentApp.ParagraphHeading.NORMAL,
      leftIndent: 0,
      rightIndent: 0,
      alignment: DocumentApp.HorizontalAlignment.CENTER,
      color: COLOR_DEFAULT,
    },
    addBlankLine: true,
    exitDialog: true,
  },
  INSTRUCTION: {
    name: "instruction",
    styles: {
      heading: DocumentApp.ParagraphHeading.NORMAL,
      leftIndent: 0,
      rightIndent: 0,
      alignment: DocumentApp.HorizontalAlignment.RIGHT,
      color: COLOR_DEFAULT,
    },
    addBlankLine: true,
    exitDialog: true,
  },
  ACTION: {
    name: "action",
    styles: {
      heading: DocumentApp.ParagraphHeading.NORMAL,
      leftIndent: 0.5 * POINTS_PER_INCH,
      rightIndent: 0,
      color: COLOR_DEFAULT,
    },
    addBlankLine: true,
  },
  DIALOG: {
    name: "dialog",
    styles: {
      heading: DocumentApp.ParagraphHeading.NORMAL,
      leftIndent: 1.5 * POINTS_PER_INCH,
      rightIndent: 0.5 * POINTS_PER_INCH,
      color: COLOR_DIALOG,
    },
    addBlankLine: true,
    exitDialog: false,
  },
  DIRECTIVE: {
    name: "directive",
    styles: {
      heading: DocumentApp.ParagraphHeading.NORMAL,
      leftIndent: 0.5 * POINTS_PER_INCH,
      rightIndent: 0,
      color: COLOR_DIRECTIVE,
      fontSize: 8,
    },
    addBlankLine: true,
    removePreviousBlankLine: true,
    exitDialog: true,
  },
  MUSIC: {
    name: "music",
    styles: {
      heading: DocumentApp.ParagraphHeading.NORMAL,
      leftIndent: 0.5 * POINTS_PER_INCH,
      rightIndent: 0,
      bold: true,
    },
    addBlankLine: true,
    exitDialog: true,
  },
  DEFAULT: {
    name: "default",
  },
};


// Parse a line (paragraph) to recognize what it is
//
function getLine(body, i) {

  var child = body.getChild(i);
  if (child.getType() != DocumentApp.ElementType.PARAGRAPH) {
    return null;
  }

  var str = child.getText();

  if (str.match(/^$/)) {
    return { type: ParaTypes.BLANK, text: str };
  }

  if (str.match(/^(int|ext)[.]? /i)) {
    return { type: ParaTypes.LOCATION, text: str };
  }

  if (str.match(/^# /)) {
    return { type: ParaTypes.H2, text: str };
  }

  if (str.match(/^## /)) {
    return { type: ParaTypes.H3, text: str };
  }

  if (str.match(/^### /)) {
    return { type: ParaTypes.H4, text: str };
  }

  if (str.match(/^#### /)) {
    return { type: ParaTypes.H5, text: str };
  }

  if (str.match(/^##### /)) {
    return { type: ParaTypes.H6, text: str };
  }

  if (str.match(/^{/)) {
    return { type: ParaTypes.DIRECTIVE, text: str };
  }

  if (str.match(/^\(.*\)$/)) {
    return { type: ParaTypes.PARENTHETICAL, text: str };
  }

  if (str.match(/^-.*-$/)) {
    return { type: ParaTypes.CHARACTER, text: str };
  }

  if (str.match(/^>.*<$/)) {
    return { type: ParaTypes.CENTER, text: str };
  }

  if (str.match(/:$/)) {
    return { type: ParaTypes.INSTRUCTION, text: str };
  }

  if (str.match(/^MUSIC:/)) {
    return { type: ParaTypes.MUSIC, text: str };
  }

  return { type: ParaTypes.DEFAULT, text: str };
}


// Run through the document line by line, applying styles.
//
function reformatDocument() {

  var doc = DocumentApp.getActiveDocument();
  var body = doc.getBody();

  var skipIntro = true;
  var inDialog = false;
  var addedBlankLine = false;
  var i = 0;
  while (i < body.getNumChildren()) {

    var line = getLine(body, i);
    if (line && (line.type.name == ParaTypes.LOCATION.name || line.type.name == ParaTypes.H2.name)) {
      skipIntro = false;
    }

    if (skipIntro || line == null || line.type == null) {

      i++;

    } else if (line.type.name == ParaTypes.BLANK.name) {

      // Remove blank lines (we add in ones where we think they should be)
      // But cannot remove last paragraph in document.
      if (i == body.getNumChildren() - 1) {
        break;
      }
      body.removeChild(body.getChild(i));

    } else {

      // Work out when starting a new stanza, exiting dialog mode
      if (line.type.name == ParaTypes.CHARACTER.name) {
        inDialog = true;
      }
      if (line.type.exitDialog) {
        inDialog = false;
      }
      if (body.getChild(i).getNumChildren() > 0) {
        var text = body.getChild(i).getChild(0);
        var m = text.getText().match(/^\[.*\]/);
        if (m) {
          inDialog = false;
        }
      }

      // General formatting rules apply.
      var type = line.type;
      if (type.name == ParaTypes.DEFAULT.name) {
        type = inDialog ? ParaTypes.DIALOG : ParaTypes.ACTION;
      }

      if (type.removePreviousBlankLine && addedBlankLine) {
        body.removeChild(body.getChild(i -1));
        i--;
      }
      applyStyles(body.getChild(i++), type.styles);
      if (type.addBlankLine) {
        addBlankLine(body, i++);
      }
      addedBlankLine = type.addBlankLine;

    }
  }
}


// Add a blank line at the specified index.
//
function addBlankLine(body, index) {
  var newPara = body.insertParagraph(index, "");
  newPara.setHeading(DocumentApp.ParagraphHeading.NORMAL);
  newPara.setIndentFirstLine(0);
  newPara.setIndentStart(0);
  newPara.setIndentEnd(0);
  newPara.setForegroundColor(COLOR_DEFAULT);
}


// Apply styles to existing paragraph.
//
function applyStyles(para, styles) {

  if (styles.heading != undefined) {
    para.setHeading(styles.heading);
  }
  if (styles.leftIndent != undefined) {
    para.setIndentFirstLine(styles.leftIndent);
    para.setIndentStart(styles.leftIndent);
  }
  if (styles.rightIndent != undefined) {
    para.setIndentEnd(styles.rightIndent);
  }
  if (styles.alignment != undefined) {
    para.setAlignment(styles.alignment);
  }
  if (styles.color != undefined) {
    para.setForegroundColor(styles.color);
  }
  if (styles.bold != undefined) {
    for (var i = 0; i < para.getNumChildren(); i++) {
      para.getChild(i).setBold(styles.bold);
    }
  }
  if (styles.fontSize != undefined) {
    for (var i = 0; i < para.getNumChildren(); i++) {
      para.getChild(i).setFontSize(styles.fontSize);
    }
  }

  // Look for [...] at start of paragraph.
  if (para.getNumChildren() > 0) {
    var text = para.getChild(0);
    var m = text.getText().match(/^\[.*\]/);
    if (m) {
      text.setBold(0, m[0].length - 1, true);
    }
  }
}
