#!/usr/bin/env python3
# SteamDB data fetcher via Chrome DevTools Protocol
import asyncio
import websockets
import json
import urllib.request
import sys
import os

async def get_steamdb_data():
    output_dir = r"Q:\数据\Web\infi\release\AI_Prompt_with_Powershell"
    
    # List pages
    with urllib.request.urlopen('http://127.0.0.1:9222/json/list') as response:
        pages = json.loads(response.read())
    
    # Find steamdb page, or use first page
    page = [p for p in pages if 'steamdb' in p.get('url', '')]
    if page:
        page_id = page[0]['id']
    else:
        # Refresh to the depots page
        page_id = pages[0]['id']
    
    uri = f'ws://127.0.0.1:9222/devtools/page/{page_id}'
    
    async with websockets.connect(uri, max_size=10*1024*1024) as ws:
        # Step 1: Evaluate - navigate to depots page first
        await ws.send(json.dumps({
            'id': 1,
            'method': 'Page.navigate',
            'params': {'url': 'https://steamdb.info/app/3164330/depots/'}
        }))
        resp = await ws.recv()
        data = json.loads(resp)
        print(f"[CDP] Navigated to depots page: {data.get('result', {}).get('url', 'unknown')}", flush=True)
        
        # Wait for page load
        await asyncio.sleep(6)
        
        # Step 2: Get depots page content
        await ws.send(json.dumps({
            'id': 2,
            'method': 'Runtime.evaluate',
            'params': {'expression': 'document.body.innerText'}
        }))
        resp = await ws.recv()
        data = json.loads(resp)
        depots_text = data['result']['result']['value']
        
        depots_path = os.path.join(output_dir, 'steamdb_depots.txt')
        with open(depots_path, 'w', encoding='utf-8') as f:
            f.write(depots_text)
        print(f"[CDP] Saved depots page content ({len(depots_text)} chars)", flush=True)
        
        # Step 3: Navigate to manifests page
        await ws.send(json.dumps({
            'id': 3,
            'method': 'Page.navigate',
            'params': {'url': 'https://steamdb.info/depot/3164332/manifests/'}
        }))
        resp = await ws.recv()
        print(f"[CDP] Navigated to manifests page", flush=True)
        await asyncio.sleep(5)
        
        # Step 4: Get manifests page content
        await ws.send(json.dumps({
            'id': 4,
            'method': 'Runtime.evaluate',
            'params': {'expression': 'document.body.innerText'}
        }))
        resp = await ws.recv()
        data = json.loads(resp)
        manifests_text = data['result']['result']['value']
        
        manifests_path = os.path.join(output_dir, 'steamdb_manifests.txt')
        with open(manifests_path, 'w', encoding='utf-8') as f:
            f.write(manifests_text)
        print(f"[CDP] Saved manifests page content ({len(manifests_text)} chars)", flush=True)
        
        print('OK', flush=True)

try:
    asyncio.run(get_steamdb_data())
except Exception as e:
    print(f'ERROR: {e}', flush=True)
    sys.exit(1)
