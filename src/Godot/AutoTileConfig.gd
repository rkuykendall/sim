class_name AutoTileConfig

# Standard 47-tile autotiling template.
# Bitmask: bit0=TL, bit1=T, bit2=TR, bit3=L, bit4=R, bit5=BL, bit6=B, bit7=BR

# Each entry: [atlas_x, atlas_y, peering_bits]
const STANDARD_47_TILE_PATTERNS: Array = [
	# Row 0
	[0,  0, 0x40],
	[1,  0, 0x50],
	[2,  0, 0x58],
	[3,  0, 0x48],
	[4,  0, 0x5B],
	[5,  0, 0xD8],
	[6,  0, 0x78],
	[7,  0, 0x5E],
	[8,  0, 0xD0],
	[9,  0, 0xFA],
	[10, 0, 0xF8],
	[11, 0, 0x68],
	# Row 1
	[0,  1, 0x42],
	[1,  1, 0x52],
	[2,  1, 0x5A],
	[3,  1, 0x4A],
	[4,  1, 0xD2],
	[5,  1, 0xFE],
	[6,  1, 0xFB],
	[7,  1, 0x6A],
	[8,  1, 0xD6],
	[9,  1, 0x7E],
	[11, 1, 0x7B],
	# Row 2
	[0,  2, 0x02],
	[1,  2, 0x12],
	[2,  2, 0x1A],
	[3,  2, 0x0A],
	[4,  2, 0x56],
	[5,  2, 0xDF],
	[6,  2, 0x7F],
	[7,  2, 0x4B],
	[8,  2, 0xDE],
	[9,  2, 0xFF],
	[10, 2, 0xDB],
	[11, 2, 0x6B],
	# Row 3
	[0,  3, 0x00],
	[1,  3, 0x10],
	[2,  3, 0x18],
	[3,  3, 0x08],
	[4,  3, 0x7A],
	[5,  3, 0x1E],
	[6,  3, 0x1B],
	[7,  3, 0xDA],
	[8,  3, 0x16],
	[9,  3, 0x1F],
	[10, 3, 0x5F],
	[11, 3, 0x0B],
]
