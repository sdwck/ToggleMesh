import os
import time
from togglemesh.client import ToggleMeshClient
from togglemesh.models import ToggleMeshOptions

def main():
    API_KEY = os.environ.get("TOGGLEMESH_API_KEY")
    if not API_KEY:
        print("TOGGLEMESH_API_KEY environment variable is required.")
        exit(1)

    options = ToggleMeshOptions(
        base_url="http://localhost:5264",
        server_key=API_KEY,
        disable_ssl_verification=False
    )

    client = ToggleMeshClient(options)
    print("Python SDK started. Simulating traffic...")

    try:
        while True:
            user_id = "user_python_1"
            context = {"Browser": "Firefox"}

            variation = client.get_variation("mab-string-test", "default-variant", identity=user_id, context=context)
            print(f"[Python SDK] Evaluated mab-string-test for {user_id}: {variation}")

            if int(time.time() * 1000) % 3 == 0:
                client.track("purchase", identity=user_id, properties={"sdk": "python"}, value=15.0)
                print(f"[Python SDK] Tracked 'purchase' for {user_id}")

            time.sleep(1.5)
    except KeyboardInterrupt:
        print("Exiting...")
        client.close()

if __name__ == "__main__":
    main()
