package togglemesh

import (
	"regexp"
	"strconv"
	"strings"
)

type RuleOperator interface {
	Evaluate(contextValue string, compiledValue any) bool
}

var operatorMap = map[string]RuleOperator{
	"equals":                 &EqualsOperator{},
	"notequals":              &NotEqualsOperator{},
	"greaterthan":            &GreaterThanOperator{},
	"greaterthanorequal":     &GreaterThanOrEqualOperator{},
	"lessthan":               &LessThanOperator{},
	"lessthanorequal":        &LessThanOrEqualOperator{},
	"contains":               &ContainsOperator{},
	"startswith":             &StartsWithOperator{},
	"endswith":               &EndsWithOperator{},
	"regex":                  &RegexOperator{},
}

func getOperator(name string) RuleOperator {
	if op, ok := operatorMap[strings.ToLower(name)]; ok {
		return op
	}
	return &FalseOperator{}
}

func CompileValue(opName string, value string) any {
	opName = strings.ToLower(opName)
	switch opName {
	case "greaterthan", "greaterthanorequal", "lessthan", "lessthanorequal":
		if f, err := strconv.ParseFloat(value, 64); err == nil {
			return f
		}
		return value
	case "regex":
		if r, err := regexp.Compile(value); err == nil {
			return r
		}
		return nil
	default:
		return value
	}
}

type FalseOperator struct{}
func (o *FalseOperator) Evaluate(ctx string, val any) bool { return false }

type EqualsOperator struct{}
func (o *EqualsOperator) Evaluate(ctx string, val any) bool { return ctx == val.(string) }

type NotEqualsOperator struct{}
func (o *NotEqualsOperator) Evaluate(ctx string, val any) bool { return ctx != val.(string) }

type ContainsOperator struct{}
func (o *ContainsOperator) Evaluate(ctx string, val any) bool { return strings.Contains(ctx, val.(string)) }

type StartsWithOperator struct{}
func (o *StartsWithOperator) Evaluate(ctx string, val any) bool { return strings.HasPrefix(ctx, val.(string)) }

type EndsWithOperator struct{}
func (o *EndsWithOperator) Evaluate(ctx string, val any) bool { return strings.HasSuffix(ctx, val.(string)) }

type RegexOperator struct{}
func (o *RegexOperator) Evaluate(ctx string, val any) bool {
	if r, ok := val.(*regexp.Regexp); ok && r != nil {
		return r.MatchString(ctx)
	}
	return false
}

func compareFloat(ctx string, val any, comp func(a, b float64) bool) bool {
	ctxFloat, err := strconv.ParseFloat(ctx, 64)
	if err != nil {
		return false
	}
	valFloat, ok := val.(float64)
	if !ok {
		return false
	}
	return comp(ctxFloat, valFloat)
}

type GreaterThanOperator struct{}
func (o *GreaterThanOperator) Evaluate(ctx string, val any) bool {
	return compareFloat(ctx, val, func(a, b float64) bool { return a > b })
}

type GreaterThanOrEqualOperator struct{}
func (o *GreaterThanOrEqualOperator) Evaluate(ctx string, val any) bool {
	return compareFloat(ctx, val, func(a, b float64) bool { return a >= b })
}

type LessThanOperator struct{}
func (o *LessThanOperator) Evaluate(ctx string, val any) bool {
	return compareFloat(ctx, val, func(a, b float64) bool { return a < b })
}

type LessThanOrEqualOperator struct{}
func (o *LessThanOrEqualOperator) Evaluate(ctx string, val any) bool {
	return compareFloat(ctx, val, func(a, b float64) bool { return a <= b })
}

type InSegmentOperator struct {
	Engine *RuleEngine
}
func (o *InSegmentOperator) Evaluate(ctx string, val any) bool {
	return false
}
