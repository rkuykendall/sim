class_name TimeService

const TICKS_PER_MINUTE: int = 10
const MINUTES_PER_HOUR: int = 60
const HOURS_PER_DAY: int = 24
const TICKS_PER_HOUR: int = TICKS_PER_MINUTE * MINUTES_PER_HOUR
const TICKS_PER_DAY: int = TICKS_PER_HOUR * HOURS_PER_DAY
const DEFAULT_START_HOUR: int = 8

var tick: int = 0


func _init(start_hour: int = DEFAULT_START_HOUR) -> void:
	tick = start_hour * TICKS_PER_HOUR


var total_minutes: int:
	get: return tick / TICKS_PER_MINUTE

var minute: int:
	get: return total_minutes % MINUTES_PER_HOUR

var hour: int:
	get: return (tick / TICKS_PER_HOUR) % HOURS_PER_DAY

var day: int:
	get: return (tick / TICKS_PER_DAY) + 1

var is_night: bool:
	get: return hour < 6 or hour >= 22  # 10 PM – 6 AM

var is_sleep_time: bool:
	get: return hour < 6 or hour >= 23  # 11 PM – 6 AM

var time_string: String:
	get: return "Day %d, %02d:%02d" % [day, hour, minute]


func advance_tick() -> void:
	tick += 1


func set_tick(t: int) -> void:
	tick = t
