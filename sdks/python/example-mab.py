import os
import uuid
import random
import time
from togglemesh.client import ToggleMeshClient
from togglemesh.models import ToggleMeshOptions

client = ToggleMeshClient(ToggleMeshOptions(base_url="http://localhost:5264", server_key=os.getenv("TOGGLEMESH_API_KEY"), disable_ssl_verification=False))

def main():
    print("Python SDK MAB Simulation started (Firefox -> Second)")

    while True:
        try:
            user_id = str(uuid.uuid4())
            variation = client.get_variation("mab-string-test", "default-variant", identity=user_id, Browser="Firefox")
            prob = 0.155 if variation == "Second" else 0.145
            
            print(f"[Python SDK] Evaluated mab-string-test for {user_id}: {variation}")
            
            if random.random() < prob:
                client.track("purchase", identity=user_id, sdk="python", value=10.0)
        except Exception as e:
            print(f"Error: {e}")
        time.sleep(1)

if __name__ == "__main__":
    main()
