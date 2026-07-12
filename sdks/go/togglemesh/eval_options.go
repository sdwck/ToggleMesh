package togglemesh

type evalOptions struct {
	Identity string
	Context  map[string]any
	Value    *float64
}

type EvalOption func(*evalOptions)

func WithIdentity(identity string) EvalOption {
	return func(o *evalOptions) {
		o.Identity = identity
	}
}

func WithContext(ctx map[string]any) EvalOption {
	return func(o *evalOptions) {
		o.Context = ctx
	}
}

func WithEventValue(value float64) EvalOption {
	return func(o *evalOptions) {
		o.Value = &value
	}
}

func applyOptions(opts []EvalOption) *evalOptions {
	options := &evalOptions{
		Context: make(map[string]any),
	}
	for _, opt := range opts {
		opt(options)
	}
	return options
}
