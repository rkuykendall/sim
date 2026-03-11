class_name SoundManager
extends Node

const _UI_CLICK_PATH:    String = "res://audio/sfx/ui_click.ogg"
const _UI_SELECT_PATH:   String = "res://audio/sfx/ui_select.ogg"
const _PAINT_PATH:       String = "res://audio/sfx/paint.ogg"
const _PAINT_TICK_PATH:  String = "res://audio/sfx/paint_tick.ogg"
const _BUILD_PATH:       String = "res://audio/sfx/build.ogg"
const _DELETE_PATH:      String = "res://audio/sfx/delete.ogg"

var _ui_player: AudioStreamPlayer
var _action_player: AudioStreamPlayer
var _soft_player: AudioStreamPlayer

var _ui_click: AudioStream
var _ui_select: AudioStream
var _paint: AudioStream
var _paint_tick: AudioStream
var _build: AudioStream
var _delete: AudioStream


func _ready() -> void:
	_ui_player = AudioStreamPlayer.new()
	add_child(_ui_player)

	_action_player = AudioStreamPlayer.new()
	add_child(_action_player)

	_soft_player = AudioStreamPlayer.new()
	_soft_player.volume_db = -15.0
	add_child(_soft_player)

	_ui_click   = _try_load(_UI_CLICK_PATH)
	_ui_select  = _try_load(_UI_SELECT_PATH)
	_paint      = _try_load(_PAINT_PATH)
	_paint_tick = _try_load(_PAINT_TICK_PATH)
	_build      = _try_load(_BUILD_PATH)
	_delete     = _try_load(_DELETE_PATH)


func play_click() -> void:
	_play_on(_ui_player, _ui_click)

func play_select() -> void:
	_play_on(_ui_player, _ui_select)

func play_paint() -> void:
	_play_on(_action_player, _paint)

func play_paint_tick() -> void:
	_play_on(_soft_player, _paint_tick)

func play_build() -> void:
	_play_on(_action_player, _build)

func play_delete() -> void:
	_play_on(_action_player, _delete)


func _play_on(player: AudioStreamPlayer, stream: AudioStream) -> void:
	if stream == null or player == null:
		return
	player.stream = stream
	player.play()


func _try_load(path: String) -> AudioStream:
	if ResourceLoader.exists(path):
		return load(path)
	return null
