class_name DefUtils

## Typed accessors for reading fields off content-def dictionaries (building/need/terrain defs
## loaded from JSON — see ContentLoader). Dictionary.get() on an untyped Dictionary always
## returns Variant, which the GDScript static analyzer correctly flags as unsafe wherever it
## feeds into a typed constructor or assignment — that's real signal everywhere else in the
## codebase, but content defs are dynamically-typed by design (they come straight from JSON).
## Centralizing the one unavoidable unsafe conversion here (each @warning_ignore'd) means every
## other call site gets a cleanly-typed value instead of repeating the same suppression everywhere.


static func get_int(def: Dictionary, key: String, default: int = 0) -> int:
	@warning_ignore("unsafe_call_argument")
	return int(def.get(key, default))


static func get_float(def: Dictionary, key: String, default: float = 0.0) -> float:
	@warning_ignore("unsafe_call_argument")
	return float(def.get(key, default))


static func get_bool(def: Dictionary, key: String, default: bool = false) -> bool:
	@warning_ignore("unsafe_call_argument")
	return bool(def.get(key, default))


static func get_string(def: Dictionary, key: String, default: String = "") -> String:
	@warning_ignore("unsafe_call_argument")
	return String(def.get(key, default))


static func get_array(def: Dictionary, key: String, default: Array = []) -> Array:
	@warning_ignore("unsafe_cast")
	return def.get(key, default) as Array


static func get_dict(def: Dictionary, key: String, default: Dictionary = {}) -> Dictionary:
	@warning_ignore("unsafe_cast")
	return def.get(key, default) as Dictionary
