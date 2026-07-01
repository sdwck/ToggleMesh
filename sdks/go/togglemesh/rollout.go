package togglemesh

import (
	"hash/fnv"
)

func evaluateRollout(rolloutPercentage *int, flagKey, identity string) bool {
	if rolloutPercentage == nil {
		return true
	}
	if *rolloutPercentage <= 0 {
		return false
	}
	if *rolloutPercentage >= 100 {
		return true
	}
	if identity == "" {
		return false
	}

	hasher := fnv.New32a()
	_, _ = hasher.Write([]byte(flagKey + identity))
	hashVal := hasher.Sum32()
	
	bucket := hashVal % 100
	return bucket < uint32(*rolloutPercentage)
}
