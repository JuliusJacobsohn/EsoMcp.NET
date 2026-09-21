"""Exercise the real stdio MCP server. Synthetic data by default; optional local database.

python scripts/smoke_mcp.py [--dll PATH] [--live-database PATH --saved-variables DIR --addons DIR]
No game files are written. The optional live mode explicitly refreshes the specified database.
"""
import argparse
import json
from pathlib import Path
import queue
import subprocess
import tempfile
import threading


class McpClient:
    def __init__(self, command):
        self.log = tempfile.TemporaryFile(mode="w+t", encoding="utf-8")
        self.process = subprocess.Popen(command, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                        stderr=self.log, text=True, encoding="utf-8", bufsize=1)
        self.responses = queue.Queue()
        self.next_id = 0
        threading.Thread(target=self._read, daemon=True).start()

    def _read(self):
        try:
            for line in self.process.stdout:
                self.responses.put(json.loads(line))
        except Exception as error:
            self.responses.put(error)
        finally:
            self.responses.put(EOFError("Server closed stdout"))

    def send(self, message):
        self.process.stdin.write(json.dumps(message) + "\n")
        self.process.stdin.flush()

    def request(self, method, params=None):
        self.next_id += 1
        self.send({"jsonrpc": "2.0", "id": self.next_id, "method": method, "params": params or {}})
        while True:
            response = self.responses.get(timeout=30)
            if isinstance(response, Exception):
                raise response
            if response.get("id") != self.next_id:
                continue
            assert "error" not in response, response
            return response["result"]

    def call(self, name, arguments=None, expect_error=False):
        result = self.request("tools/call", {"name": name, "arguments": arguments or {}})
        assert bool(result.get("isError")) == expect_error, result
        if expect_error:
            return result
        text = "".join(c["text"] for c in result["content"] if c["type"] == "text")
        return json.loads(text)

    def close(self):
        self.process.stdin.close()
        try:
            self.process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            self.process.kill()
            self.process.wait(timeout=5)
        self.process.stdout.close()
        self.log.seek(0)
        log = self.log.read()
        self.log.close()
        return log


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--dll", default="src/EsoMcp.Server/bin/Release/net10.0/eso-mcp.dll")
    parser.add_argument("--live-database")
    parser.add_argument("--saved-variables")
    parser.add_argument("--addons")
    args = parser.parse_args()
    with tempfile.TemporaryDirectory(prefix="eso-mcp-smoke-") as temp:
        saved = Path(args.saved_variables or temp)
        database = args.live_database or str(Path(temp) / "smoke.db")
        if not args.live_database:
            assert not args.saved_variables and not args.addons, "Source overrides require --live-database"
            (saved / "CarosSkillPointSaver.lua").write_text("""
                CSPSSavedVariables={['EU Megaserver']={['@Synthetic']={['$AccountWide']={charData={['123']={
                  ['$lastCharacterName']='Synthetic Character',profiles={[1]={name='Synthetic Build',
                  werte={prog={part1='900100:0'},pass={part1='900200:2'}},
                  comp1='64;0;0#900100,-,-,-,-,-#100-20#100,-,-,-;-,-,-,-;-,-,-,-#-#2#0',comp2='-#-#-'}
                }}}}}}}
                """, encoding="utf-8")
        command = ["dotnet", str(Path(args.dll).resolve()), "--database", database, "--saved-variables", str(saved)]
        if args.addons:
            command += ["--addons", args.addons]
        client = McpClient(command)
        try:
            initialized = client.request("initialize", {"protocolVersion": "2025-11-25", "capabilities": {},
                                                        "clientInfo": {"name": "EsoMcp smoke test", "version": "1.0"}})
            client.send({"jsonrpc": "2.0", "method": "notifications/initialized"})
            tools = client.request("tools/list")["tools"]
            assert len(tools) == 13, [t["name"] for t in tools]
            # A normal query must populate an empty database without an explicit refresh.
            characters = client.call("list_characters")
            assert characters["rows"], characters
            result = client.call("refresh_database", {"force": True})
            assert not any(s["status"] == "failed" for s in result["sources"]), result
            status = client.call("database_status")
            characters = client.call("list_characters")
            assert characters["rows"], characters
            records = client.call("list_records", {"kind": "build"})
            assert records["rows"], records
            record_key = records["rows"][0]["record_key"]
            record = client.call("get_record", {"recordKey": record_key})
            exported = client.call("export_saved_build", {"recordKey": record_key})
            assert exported["text"] == record["nativeText"]
            assert exported["appliedInGame"] is False
            for name in ("search_inventory", "get_knowledge", "find_sets", "find_item_definitions", "find_skill_definitions"):
                page = client.call(name, {"limit": 2})
                assert len(page["rows"]) <= 2 and page["limit"] == 2
            crafted = client.call("create_crafting_import", {"itemIds": [900001], "level": 32, "quality": 4})
            assert "item:900001:23:32:" in crafted["text"]
            client.call("create_crafting_import", {"itemIds": [900001], "level": 33, "quality": 4}, expect_error=True)
            client.call("list_characters", {"limit": 1000}, expect_error=True)
            client.call("get_record", {"recordKey": "does-not-exist"}, expect_error=True)
            if not args.live_database:
                # New fields are queryable through MCP, including their source/observation metadata.
                observation = saved / "uespLog.lua"
                observation.write_text("""
                    uespLogSavedVars={CharName='Synthetic Character',CharId='123',WorldName='EU',
                      AccountName='@Synthetic',TimeStamp=100,Level=50,Skills={['Craft:Clothing']=50},
                      Research={Timestamp=90,['Clothier:Trait:Known']=120,['Clothier:Trait:Total']=126},
                      ChampionPoints2={['Total:Unspent']=9,Slots={[1]=66,[2]=0}},Stats={Health=40000}}
                    """, encoding="utf-8")
                key = characters["rows"][0]["character_key"]
                research = client.call("get_character_state", {"characterKey": key, "section": "research"})
                assert research["available"] and research["data"]["crafts"][0]["knownTraits"] == 120
                assert research["observation"]["name"] == "Synthetic Character"
                cp = client.call("get_character_state", {"characterKey": key, "section": "champion"})
                assert cp["data"]["slots"]["2"] == 0 and cp["data"]["unspentPoints"] == 9
                stats = client.call("get_character_state", {"characterKey": key, "section": "statistics"})
                assert stats["data"]["current"]["values"]["Health"] == 40000
                summary = client.call("get_character_state", {"characterKey": key})
                assert "research" in summary["data"]["availableSections"]
                assert "equipment" not in summary["data"]["availableSections"]
                client.call("get_character_state", {"characterKey": key, "section": "invalid"}, expect_error=True)
                assert not client.call("get_character_state", {"characterKey": "unknown"})["available"]
                observation.unlink()
                assert client.call("get_character_state", {"characterKey": key, "section": "research"})["available"]
                source = saved / "CarosSkillPointSaver.lua"
                changed = source.read_text(encoding="utf-8").replace("Synthetic Character", "Updated Character")
                source.write_text(changed, encoding="utf-8")
                characters = client.call("list_characters")
                assert characters["rows"][0]["name"] == "Updated Character"
                # A broken disk save must not prevent queries of the last good data.
                source.write_text("CSPSSavedVariables = {", encoding="utf-8")
                assert client.call("list_characters")["rows"] == characters["rows"]
                attempts = client.call("database_status")["lastRefresh"]
                assert any(s["label"] == "builds" and s["status"] == "failed" for s in attempts)
                source.write_text(changed, encoding="utf-8")
                # Manual mode leaves cached data alone until explicitly refreshed.
                manual = McpClient(command + ["--no-auto-refresh"])
                try:
                    manual.request("initialize", {"protocolVersion": "2025-11-25", "capabilities": {},
                                                  "clientInfo": {"name": "manual-mode-test", "version": "1.0"}})
                    manual.send({"jsonrpc": "2.0", "method": "notifications/initialized"})
                    source.write_text(changed.replace("Updated Character", "Manual Character"), encoding="utf-8")
                    assert manual.call("list_characters")["rows"][0]["name"] == "Updated Character"
                    manual.call("refresh_database")
                    assert manual.call("list_characters")["rows"][0]["name"] == "Manual Character"
                finally:
                    manual.close()
                characters = client.call("list_characters")
                source.unlink()
                assert client.call("list_characters")["rows"] == characters["rows"]
            print(json.dumps({"protocol": initialized["protocolVersion"], "tools": len(tools),
                              "counts": status["counts"], "result": "passed"}))
        except Exception:
            print(client.close())
            raise
        else:
            client.close()


if __name__ == "__main__":
    main()
