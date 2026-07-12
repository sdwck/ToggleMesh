package togglemesh

import (
	"hash/fnv"
)

func evaluateRollout(rollout []VariationWeight, flagKey, identity string) *string {
	if len(rollout) == 0 {
		return nil
	}
	if len(rollout) == 1 {
		return &rollout[0].VariationID
	}
	if identity == "" {
		return &rollout[0].VariationID
	}

	hasher := fnv.New32a()
	_, _ = hasher.Write([]byte(flagKey + identity))
	hashVal := hasher.Sum32()
	
	bucket := hashVal % 10000
	
	sum := uint32(0)
	for _, w := range rollout {
		sum += uint32(w.Weight)
		if bucket < sum {
			return &w.VariationID
		}
	}
	
	return &rollout[len(rollout)-1].VariationID
}
