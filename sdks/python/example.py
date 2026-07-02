import time
import logging
import urllib3
from togglemesh import ToggleMeshClient, ToggleMeshOptions

logging.basicConfig(level=logging.INFO)
urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

def main():
    options = ToggleMeshOptions(
        base_url="http://localhost:5264",
        client_key="tm_server_w9pYCQFCJj3DdXuzsQfWvZNSIjE1x0zrMSv5PHvrW8",
        disable_ssl_verification=True
    )
    
    client = ToggleMeshClient(options)
    
    try:
        print("Starting flag evaluation loop (press Ctrl+C to exit)...")
        while True:
            email = "nirawolker@gmail.com"
            uid = "123456"
            context = {"Email": email, "UserId": uid}
            
            is_enabled = client.is_enabled("gmail-20percent", identity=uid, context=context)
            
            if is_enabled:
                client.track("python_gmail_checked", properties=context, value=1.0, identity=uid)
                print(f"[Gmail 20%] {email} -> ENABLED!")
            else:
                print(f"[Gmail 20%] {email} -> DISABLED.")
                
            time.sleep(3)
    except KeyboardInterrupt:
        print("Stopping...")
    finally:
        client.stop()

if __name__ == "__main__":
    main()
