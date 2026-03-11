class_name SimTestCase
extends RefCounted

var _fail_msgs: Array = []
var _pass_count: int = 0
var _fail_count: int = 0


func assert_true(cond: bool, msg: String = "") -> void:
	if not cond:
		_fail_msgs.append("assert_true failed" + (" — " + msg if msg else ""))


func assert_false(cond: bool, msg: String = "") -> void:
	if cond:
		_fail_msgs.append("assert_false failed" + (" — " + msg if msg else ""))


func assert_eq(a: Variant, b: Variant, msg: String = "") -> void:
	if a != b:
		_fail_msgs.append("assert_eq: expected %s == %s%s" % [str(b), str(a), " — " + msg if msg else ""])


func assert_not_eq(a: Variant, b: Variant, msg: String = "") -> void:
	if a == b:
		_fail_msgs.append("assert_not_eq: expected %s != %s%s" % [str(a), str(b), " — " + msg if msg else ""])


func assert_gt(a: float, b: float, msg: String = "") -> void:
	if not (a > b):
		_fail_msgs.append("assert_gt: expected %f > %f%s" % [a, b, " — " + msg if msg else ""])


func assert_lt(a: float, b: float, msg: String = "") -> void:
	if not (a < b):
		_fail_msgs.append("assert_lt: expected %f < %f%s" % [a, b, " — " + msg if msg else ""])


func assert_ge(a: float, b: float, msg: String = "") -> void:
	if not (a >= b):
		_fail_msgs.append("assert_ge: expected %f >= %f%s" % [a, b, " — " + msg if msg else ""])


func assert_approx(a: float, b: float, eps: float = 0.01, msg: String = "") -> void:
	if abs(a - b) > eps:
		_fail_msgs.append("assert_approx: |%f - %f| > %f%s" % [a, b, eps, " — " + msg if msg else ""])


func assert_not_null(val: Variant, msg: String = "") -> void:
	if val == null:
		_fail_msgs.append("assert_not_null failed — value is null" + (" — " + msg if msg else ""))


func run_test(name: String, callable: Callable) -> bool:
	_fail_msgs.clear()
	callable.call()
	if _fail_msgs.is_empty():
		print("    PASS: %s" % name)
		_pass_count += 1
		return true
	print("    FAIL: %s" % name)
	for msg in _fail_msgs:
		print("      !! %s" % msg)
	_fail_count += 1
	return false


func run_all() -> Dictionary:
	return {"pass": _pass_count, "fail": _fail_count}
