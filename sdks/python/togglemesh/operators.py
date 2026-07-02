import re
from datetime import datetime
from typing import Any, Optional, Set
from packaging.version import Version, InvalidVersion

class RuleOperator:
    @property
    def name(self) -> str:
        raise NotImplementedError()

    def compile(self, rule_value: str) -> Any:
        return rule_value

    def evaluate(self, user_value: str, compiled_rule_value: Any) -> bool:
        raise NotImplementedError()


class EqualsOperator(RuleOperator):
    @property
    def name(self) -> str: return "Equals"
    def evaluate(self, user_value: str, compiled_rule_value: Any) -> bool:
        return user_value == compiled_rule_value

class NotEqualsOperator(RuleOperator):
    @property
    def name(self) -> str: return "NotEquals"
    def evaluate(self, user_value: str, compiled_rule_value: Any) -> bool:
        return user_value != compiled_rule_value

class ContainsOperator(RuleOperator):
    @property
    def name(self) -> str: return "Contains"
    def evaluate(self, user_value: str, compiled_rule_value: Any) -> bool:
        return str(compiled_rule_value) in user_value

class StartsWithOperator(RuleOperator):
    @property
    def name(self) -> str: return "StartsWith"
    def evaluate(self, user_value: str, compiled_rule_value: Any) -> bool:
        return user_value.startswith(str(compiled_rule_value))

class EndsWithOperator(RuleOperator):
    @property
    def name(self) -> str: return "EndsWith"
    def evaluate(self, user_value: str, compiled_rule_value: Any) -> bool:
        return user_value.endswith(str(compiled_rule_value))

class InListOperator(RuleOperator):
    @property
    def name(self) -> str: return "InList"
    def compile(self, rule_value: str) -> Set[str]:
        return {x.strip() for x in rule_value.split(",")} if rule_value else set()
    def evaluate(self, user_value: str, compiled_rule_value: Any) -> bool:
        return user_value in compiled_rule_value

class RegexOperator(RuleOperator):
    @property
    def name(self) -> str: return "Regex"
    def compile(self, rule_value: str) -> Any:
        try:
            return re.compile(rule_value)
        except Exception:
            return None
    def evaluate(self, user_value: str, compiled_rule_value: Any) -> bool:
        if compiled_rule_value is None:
            return False
        return compiled_rule_value.match(user_value) is not None

class NumberOperatorBase(RuleOperator):
    def compile(self, rule_value: str) -> Any:
        try:
            return float(rule_value)
        except ValueError:
            return None

class GreaterThanOperator(NumberOperatorBase):
    @property
    def name(self) -> str: return "GreaterThan"
    def evaluate(self, user_value: str, compiled_rule_value: Any) -> bool:
        if compiled_rule_value is None: return False
        try:
            return float(user_value) > compiled_rule_value
        except ValueError:
            return False

class GreaterThanOrEqualOperator(NumberOperatorBase):
    @property
    def name(self) -> str: return "GreaterThanOrEqual"
    def evaluate(self, user_value: str, compiled_rule_value: Any) -> bool:
        if compiled_rule_value is None: return False
        try:
            return float(user_value) >= compiled_rule_value
        except ValueError:
            return False

class LessThanOperator(NumberOperatorBase):
    @property
    def name(self) -> str: return "LessThan"
    def evaluate(self, user_value: str, compiled_rule_value: Any) -> bool:
        if compiled_rule_value is None: return False
        try:
            return float(user_value) < compiled_rule_value
        except ValueError:
            return False

class LessThanOrEqualOperator(NumberOperatorBase):
    @property
    def name(self) -> str: return "LessThanOrEqual"
    def evaluate(self, user_value: str, compiled_rule_value: Any) -> bool:
        if compiled_rule_value is None: return False
        try:
            return float(user_value) <= compiled_rule_value
        except ValueError:
            return False

class DateOperatorBase(RuleOperator):
    def compile(self, rule_value: str) -> Any:
        try:
            return datetime.fromisoformat(rule_value.replace('Z', '+00:00'))
        except ValueError:
            return None

class DateAfterOperator(DateOperatorBase):
    @property
    def name(self) -> str: return "DateAfter"
    def evaluate(self, user_value: str, compiled_rule_value: Any) -> bool:
        if compiled_rule_value is None: return False
        try:
            uv = datetime.fromisoformat(user_value.replace('Z', '+00:00'))
            return uv > compiled_rule_value
        except ValueError:
            return False

class DateBeforeOperator(DateOperatorBase):
    @property
    def name(self) -> str: return "DateBefore"
    def evaluate(self, user_value: str, compiled_rule_value: Any) -> bool:
        if compiled_rule_value is None: return False
        try:
            uv = datetime.fromisoformat(user_value.replace('Z', '+00:00'))
            return uv < compiled_rule_value
        except ValueError:
            return False

class SemVerOperatorBase(RuleOperator):
    def compile(self, rule_value: str) -> Any:
        try:
            return Version(rule_value)
        except InvalidVersion:
            return None

class SemVerEqualOperator(SemVerOperatorBase):
    @property
    def name(self) -> str: return "SemVerEqual"
    def evaluate(self, user_value: str, compiled_rule_value: Any) -> bool:
        if compiled_rule_value is None: return False
        try:
            return Version(user_value) == compiled_rule_value
        except InvalidVersion:
            return False

class SemVerGreaterThanOperator(SemVerOperatorBase):
    @property
    def name(self) -> str: return "SemVerGreaterThan"
    def evaluate(self, user_value: str, compiled_rule_value: Any) -> bool:
        if compiled_rule_value is None: return False
        try:
            return Version(user_value) > compiled_rule_value
        except InvalidVersion:
            return False

class SemVerGreaterThanOrEqualOperator(SemVerOperatorBase):
    @property
    def name(self) -> str: return "SemVerGreaterThanOrEqual"
    def evaluate(self, user_value: str, compiled_rule_value: Any) -> bool:
        if compiled_rule_value is None: return False
        try:
            return Version(user_value) >= compiled_rule_value
        except InvalidVersion:
            return False

class SemVerLessThanOperator(SemVerOperatorBase):
    @property
    def name(self) -> str: return "SemVerLessThan"
    def evaluate(self, user_value: str, compiled_rule_value: Any) -> bool:
        if compiled_rule_value is None: return False
        try:
            return Version(user_value) < compiled_rule_value
        except InvalidVersion:
            return False

class SemVerLessThanOrEqualOperator(SemVerOperatorBase):
    @property
    def name(self) -> str: return "SemVerLessThanOrEqual"
    def evaluate(self, user_value: str, compiled_rule_value: Any) -> bool:
        if compiled_rule_value is None: return False
        try:
            return Version(user_value) <= compiled_rule_value
        except InvalidVersion:
            return False


class FalseOperator(RuleOperator):
    @property
    def name(self) -> str: return "False"
    def compile(self, rule_value: str) -> Any: return None
    def evaluate(self, user_value: str, compiled_rule_value: Any) -> bool: return False

class InSegmentOperator(RuleOperator):
    @property
    def name(self) -> str: return "InSegment"
    def compile(self, rule_value: str) -> Any: return rule_value
    def evaluate(self, user_value: str, compiled_rule_value: Any) -> bool: return False

ALL_OPERATORS = [
    EqualsOperator(),
    NotEqualsOperator(),
    ContainsOperator(),
    StartsWithOperator(),
    EndsWithOperator(),
    InListOperator(),
    RegexOperator(),
    GreaterThanOperator(),
    GreaterThanOrEqualOperator(),
    LessThanOperator(),
    LessThanOrEqualOperator(),
    DateAfterOperator(),
    DateBeforeOperator(),
    SemVerEqualOperator(),
    SemVerGreaterThanOperator(),
    SemVerGreaterThanOrEqualOperator(),
    SemVerLessThanOperator(),
    SemVerLessThanOrEqualOperator()
]

OPERATOR_MAP = {op.name.lower(): op for op in ALL_OPERATORS}
