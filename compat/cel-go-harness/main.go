package main

import (
	"encoding/base64"
	"encoding/json"
	"errors"
	"flag"
	"fmt"
	"math"
	"os"
	"sort"
	"strings"
	"time"

	"github.com/google/cel-go/cel"
	"github.com/google/cel-go/common/types"
	"github.com/google/cel-go/common/types/ref"
	"github.com/google/cel-go/common/types/traits"
)

type expressionLibrary struct {
	SchemaVersion string           `json:"schemaVersion"`
	Cases         []expressionCase `json:"cases"`
}

type expressionCase struct {
	ID            string                   `json:"id"`
	Expression    string                   `json:"expression"`
	Inputs        map[string]compatValue   `json:"inputs"`
	Expected      *compatValue             `json:"expected"`
	ExpectedError *compatExpectedError     `json:"expectedError"`
}

type compatExpectedError struct {
	Category        string `json:"category"`
	MessageContains string `json:"messageContains"`
}

type compatValue struct {
	Type  string          `json:"type"`
	Value json.RawMessage `json:"value"`
}

type compatError struct {
	Category string `json:"category"`
	Message  string `json:"message,omitempty"`
}

type compatCaseResult struct {
	ID    string       `json:"id"`
	Value *compatValue `json:"value,omitempty"`
	Error *compatError `json:"error,omitempty"`
}

type compatRunOutput struct {
	Runtime       string             `json:"runtime"`
	SchemaVersion string             `json:"schemaVersion"`
	Results       []compatCaseResult `json:"results"`
}

func main() {
	libraryPath := flag.String("library", "", "path to the shared expression library JSON")
	outputPath := flag.String("output", "", "path to write evaluation results JSON")
	flag.Parse()

	if *libraryPath == "" || *outputPath == "" {
		fail("usage: go run . --library <path> --output <path>")
	}

	libraryBytes, err := os.ReadFile(*libraryPath)
	if err != nil {
		fail(err.Error())
	}

	var library expressionLibrary
	if err := json.Unmarshal(libraryBytes, &library); err != nil {
		fail(err.Error())
	}

	results := make([]compatCaseResult, 0, len(library.Cases))
	for _, testCase := range library.Cases {
		results = append(results, evaluateCase(testCase))
	}

	outputBytes, err := json.MarshalIndent(compatRunOutput{
		Runtime:       "cel-go",
		SchemaVersion: library.SchemaVersion,
		Results:       results,
	}, "", "  ")
	if err != nil {
		fail(err.Error())
	}

	if err := os.WriteFile(*outputPath, outputBytes, 0o644); err != nil {
		fail(err.Error())
	}
}

func evaluateCase(testCase expressionCase) compatCaseResult {
	envOptions := make([]cel.EnvOption, 0, len(testCase.Inputs))
	activation := make(map[string]any, len(testCase.Inputs))
	for name, input := range testCase.Inputs {
		value, err := materializeValue(input)
		if err != nil {
			return compatCaseResult{
				ID: testCase.ID,
				Error: &compatError{
					Category: "invalid_input",
					Message:  err.Error(),
				},
			}
		}

		envOptions = append(envOptions, cel.Variable(name, cel.DynType))
		activation[name] = value
	}

	envOptions = append(envOptions, cel.OptionalTypes())

	env, err := cel.NewEnv(envOptions...)
	if err != nil {
		return compatCaseResult{
			ID: testCase.ID,
			Error: &compatError{
				Category: "compile_error",
				Message:  err.Error(),
			},
		}
	}

	ast, issues := env.Compile(testCase.Expression)
	if issues != nil && issues.Err() != nil {
		return compatCaseResult{
			ID: testCase.ID,
			Error: &compatError{
				Category: "compile_error",
				Message:  issues.Err().Error(),
			},
		}
	}

	program, err := env.Program(ast)
	if err != nil {
		return compatCaseResult{
			ID: testCase.ID,
			Error: &compatError{
				Category: "program_error",
				Message:  err.Error(),
			},
		}
	}

	output, _, err := program.Eval(activation)
	if err != nil {
		return compatCaseResult{
			ID: testCase.ID,
			Error: &compatError{
				Category: categorizeError(err.Error()),
				Message:  err.Error(),
			},
		}
	}

	value, err := normalizeValue(output)
	if err != nil {
		return compatCaseResult{
			ID: testCase.ID,
			Error: &compatError{
				Category: "normalize_error",
				Message:  err.Error(),
			},
		}
	}

	return compatCaseResult{
		ID:    testCase.ID,
		Value: value,
	}
}

func materializeValue(value compatValue) (any, error) {
	switch value.Type {
	case "null":
		return nil, nil
	case "int":
		var v int64
		return v, json.Unmarshal(value.Value, &v)
	case "uint":
		var v uint64
		return v, json.Unmarshal(value.Value, &v)
	case "double":
		var raw any
		if err := json.Unmarshal(value.Value, &raw); err != nil {
			return nil, err
		}
			switch typed := raw.(type) {
			case string:
				switch typed {
				case "NaN":
					return math.NaN(), nil
				case "Infinity":
					return math.Inf(1), nil
				case "-Infinity":
					return math.Inf(-1), nil
				default:
					return nil, fmt.Errorf("unsupported double sentinel %q", typed)
				}
		case float64:
			return typed, nil
		default:
			return nil, fmt.Errorf("unsupported double value %T", raw)
		}
	case "string":
		var v string
		return v, json.Unmarshal(value.Value, &v)
	case "bool":
		var v bool
		return v, json.Unmarshal(value.Value, &v)
	case "bytes":
		var encoded string
		if err := json.Unmarshal(value.Value, &encoded); err != nil {
			return nil, err
		}
		return base64.StdEncoding.DecodeString(encoded)
	case "timestamp":
		var raw string
		if err := json.Unmarshal(value.Value, &raw); err != nil {
			return nil, err
		}
		return time.Parse(time.RFC3339Nano, raw)
	case "duration":
		var raw string
		if err := json.Unmarshal(value.Value, &raw); err != nil {
			return nil, err
		}
		return raw, nil
	case "type":
		var raw string
		return raw, json.Unmarshal(value.Value, &raw)
	case "list":
		var items []compatValue
		if err := json.Unmarshal(value.Value, &items); err != nil {
			return nil, err
		}
		result := make([]any, 0, len(items))
		for _, item := range items {
			materialized, err := materializeValue(item)
			if err != nil {
				return nil, err
			}
			result = append(result, materialized)
		}
		return result, nil
	case "map":
		var members map[string]compatValue
		if err := json.Unmarshal(value.Value, &members); err != nil {
			return nil, err
		}
		result := make(map[string]any, len(members))
		for key, member := range members {
			materialized, err := materializeValue(member)
			if err != nil {
				return nil, err
			}
			result[key] = materialized
		}
		return result, nil
	default:
		return nil, fmt.Errorf("unsupported input type %q", value.Type)
	}
}

func normalizeValue(value ref.Val) (*compatValue, error) {
	switch typed := value.(type) {
	case types.Null:
		return &compatValue{Type: "null", Value: json.RawMessage("null")}, nil
	case types.Bool:
		return scalarCompatValue("bool", bool(typed))
	case types.Int:
		return scalarCompatValue("int", int64(typed))
	case types.Uint:
		return scalarCompatValue("uint", uint64(typed))
	case types.Double:
		floatValue := float64(typed)
		switch {
		case floatValue != floatValue:
			return scalarCompatValue("double", "NaN")
		case floatValue > 0 && floatValue > 1e308:
			return scalarCompatValue("double", "Infinity")
		case floatValue < 0 && floatValue < -1e308:
			return scalarCompatValue("double", "-Infinity")
		default:
			return scalarCompatValue("double", floatValue)
		}
	case types.String:
		return scalarCompatValue("string", string(typed))
	case types.Bytes:
		return scalarCompatValue("bytes", base64.StdEncoding.EncodeToString([]byte(typed)))
	case types.Timestamp:
		return scalarCompatValue("timestamp", typed.Value().(time.Time).Format(time.RFC3339Nano))
	case types.Duration:
		return scalarCompatValue("duration", typed.Value().(time.Duration).String())
	}

	if value.Type().TypeName() == "type" {
		return scalarCompatValue("type", strings.ToLower(fmt.Sprint(value.Value())))
	}

	if list, ok := value.(traits.Lister); ok {
		iterator := list.Iterator()
		items := make([]compatValue, 0)
		for iterator.HasNext() == types.True {
			item, err := normalizeValue(iterator.Next())
			if err != nil {
				return nil, err
			}
			items = append(items, *item)
		}
		raw, err := json.Marshal(items)
		if err != nil {
			return nil, err
		}
		return &compatValue{Type: "list", Value: raw}, nil
	}

	if mapper, ok := value.(traits.Mapper); ok {
		iterator := mapper.Iterator()
		keys := make([]string, 0)
		entries := make(map[string]compatValue)
		for iterator.HasNext() == types.True {
			key := iterator.Next()
			keyString := fmt.Sprint(key.Value())
			entry, found := mapper.Find(key)
			if !found {
				return nil, errors.New("missing map entry during normalization")
			}
			normalized, err := normalizeValue(entry)
			if err != nil {
				return nil, err
			}
			keys = append(keys, keyString)
			entries[keyString] = *normalized
		}
		sort.Strings(keys)
		ordered := make(map[string]compatValue, len(keys))
		for _, key := range keys {
			ordered[key] = entries[key]
		}
		raw, err := json.Marshal(ordered)
		if err != nil {
			return nil, err
		}
		return &compatValue{Type: "map", Value: raw}, nil
	}

	return nil, fmt.Errorf("unsupported normalized value type %T", value.Value())
}

func scalarCompatValue(typeName string, value any) (*compatValue, error) {
	raw, err := json.Marshal(value)
	if err != nil {
		return nil, err
	}
	return &compatValue{Type: typeName, Value: raw}, nil
}

// categorizeError maps cel-go error messages to stable category strings.
// These patterns are based on cel-go v0.26.x error messages; if upgrading
// cel-go, verify that the message formats have not changed.
func categorizeError(message string) string {
	lower := strings.ToLower(message)
	switch {
	case strings.Contains(lower, "division by zero"):
		return "division_by_zero"
	case strings.Contains(lower, "modulo by zero"):
		return "modulo_by_zero"
	case strings.Contains(lower, "no such overload"):
		return "no_matching_overload"
	case strings.Contains(lower, "index") && strings.Contains(lower, "bounds"):
		return "index_out_of_bounds"
	case strings.Contains(lower, "invalid_argument") || strings.Contains(lower, "invalid"):
		return "invalid_argument"
	case strings.Contains(lower, "overflow"):
		return "overflow"
	default:
		return "runtime_error"
	}
}

func fail(message string) {
	fmt.Fprintln(os.Stderr, message)
	os.Exit(1)
}
