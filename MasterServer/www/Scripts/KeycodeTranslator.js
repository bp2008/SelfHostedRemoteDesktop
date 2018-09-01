// javascript keyCode: [windows keyCode, descriptive string]
var jsKeycodeDictionary =
{
	0: [220, "\\"], // Backslash

	8: [8, "\b"], // Backspace
	9: [9, "\t"], // Tab

	12: [12, "num"], // CLEAR key
	13: [13, "\n"], // ENTER key

	16: [16, "shift"], // SHIFT key
	17: [17, "meta"],  // 'ctrl' on windows, 'cmd' on mac
	18: [18, "alt"],   // Alt, aka 'option'
	19: [19, "pause"], // Pause/Break
	20: [20, "caps"], // Caps Lock

	27: [27, "esc"], // Escape

	32: [32, " "], // Space
	33: [33, "pageup"], // Page Up
	34: [34, "pagedown"], // Page Down
	35: [35, "end"],
	36: [36, "home"],
	37: [37, "left"],
	38: [38, "up"],
	39: [39, "right"],
	40: [40, "down"],

	44: [44, "print"], // Print Screen
	45: [45, "insert"], // Insert
	46: [46, "delete"], // Delete

	48: [0, "0"], // 0
	49: [1, "1"], // 1
	50: [2, "2"], // 2
	51: [3, "3"], // 3
	52: [4, "4"], // 4
	53: [5, "5"], // 5
	54: [6, "6"], // 6
	55: [7, "7"], // 7
	56: [8, "8"], // 8
	57: [9, "9"], // 9

	59: [186, ";"], // :;

	61: [187, "="], // +=

	65: [65, "a"],
	66: [66, "b"],
	67: [67, "c"],
	68: [68, "d"],
	69: [69, "e"],
	70: [70, "f"],
	71: [71, "g"],
	72: [72, "h"],
	73: [73, "i"],
	74: [74, "j"],
	75: [75, "k"],
	76: [76, "l"],
	77: [77, "m"],
	78: [78, "n"],
	79: [79, "o"],
	80: [80, "p"],
	81: [81, "q"],
	82: [82, "r"],
	83: [83, "s"],
	84: [84, "t"],
	85: [85, "u"],
	86: [86, "v"],
	87: [87, "w"],
	88: [88, "x"],
	89: [89, "y"],
	90: [90, "z"],
	91: [91, "cmd"],   // Left Windows / Left Apple
	92: [92, "cmd"],   // Right Windows
	93: [93, "cmd"],   // Context menu / Right Apple

	96: [96, "num0"],
	97: [97, "num1"],
	98: [98, "num2"],
	99: [99, "num3"],
	100: [100, "num4"],
	101: [101, "num5"],
	102: [102, "num6"],
	103: [103, "num7"],
	104: [104, "num8"],
	105: [105, "num9"],
	106: [106, "*"],
	107: [107, "+"],
	108: [13, "num_enter"], // My keyboard issues code 13 for numpad enter (same as regular enter)
	109: [109, "num_subtract"],
	110: [110, "num_decimal"],
	111: [111, "num_divide"],
	112: [112, "f1"],
	113: [113, "f2"],
	114: [114, "f3"],
	115: [115, "f4"],
	116: [116, "f5"],
	117: [117, "f6"],
	118: [118, "f7"],
	119: [119, "f8"],
	120: [120, "f9"],
	121: [121, "f10"],
	122: [122, "f11"],
	123: [123, "f12"],
	//124: [44, "print"], // Not sure why someone thought this was "print"
	
	124: [124, "f13"], // Windows has keys f13 through f24 defined
	125: [125, "f15"], // so I'm adding them here despite never
	126: [126, "f16"], // seeing a keyboard with these keys on it.
	127: [127, "f17"],
	128: [128, "f18"],
	129: [129, "f19"],
	130: [130, "f20"],
	131: [131, "f21"],
	132: [132, "f22"],
	133: [133, "f23"],
	134: [134, "f24"],
	

	144: [144, "num"],    // num lock
	145: [145, "scroll"], // scroll lock

	173: [189, "-"],

	186: [186, ";"], // :;
	187: [187, "="], // +=
	188: [188, ","], // ,<
	189: [189, "-"], // -_
	190: [190, "."], // .>
	191: [191, "/"], // /?
	192: [192, "`"], // `~
	219: [219, "["], // {[
	220: [220, "\\"], // |\
	221: [221, "]"], // }]
	222: [222, "\'"], // "'
	//223: [-1, "`"], // Not sure what this one is, so I'm un-mapping it
	224: [17, "cmd"], // Ctrl in firefox on mac
	225: [18, "alt"], // "AltGr" key.  We'll just treat it as alt

	57392: [17, "ctrl"], // Not sure of the origin of this one
	63289: [144, "num"] // Internet says this is Num Lock on safari
};
function JsKeycodeToWinKeycode(jsKeycode)
{
	var winKeycode = jsKeycodeDictionary[jsKeycode];
	if (typeof winKeycode === 'undefined')
		return -1;
	return winKeycode[0];
}