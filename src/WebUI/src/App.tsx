import { useEffect, useState } from 'react';
import './App.css';

const baseUrl = import.meta.env.VITE_BACKEND_BASE_URL || 'http://127.0.0.1:5123';

function App() {
  const [hasApiKey, setHasApiKey] = useState(false);
  const [keyInput, setKeyInput] = useState('');
  const [result, setResult] = useState<any>(null);
  const [path, setPath] = useState('');
  const [query, setQuery] = useState('');

  const reload = async () => {
    const res = await fetch(`${baseUrl}/api/status`);
    const data = await res.json();
    setHasApiKey(data.hasApiKey);
  };

  useEffect(() => {
    reload();
  }, []);

  const saveKey = async () => {
    await fetch(`${baseUrl}/api/apikey`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ key: keyInput })
    });
    setKeyInput('');
    reload();
  };

  const deleteKey = async () => {
    await fetch(`${baseUrl}/api/apikey`, { method: 'DELETE' });
    reload();
  };

  const doRequest = async (p: string) => {
    const url = `${baseUrl}/api/gw2/${p}`;
    const res = await fetch(url);
    const data = await res.json();
    setResult(data);
  };

  const genericGet = () => {
    let p = path.replace(/^\//, '');
    if (query) p += `?${query}`;
    doRequest(p);
  };

  return (
    <div className="app">
      <header>
        <img src="/assets/gw2-header.jpg" className="banner" alt="Guild Wars 2" />
        <h1>Guild Wars 2 API MCP</h1>
        <span className={`status ${hasApiKey ? 'ok' : 'bad'}`}>{hasApiKey ? 'API Key Set' : 'No API Key'}</span>
      </header>

      <section className="key-manager">
        <input type="password" value={keyInput} onChange={e => setKeyInput(e.target.value)} placeholder="API Key" />
        <button onClick={saveKey}>Save</button>
        <button onClick={deleteKey}>Delete</button>
        <button onClick={reload}>Reload</button>
      </section>

      <section className="tools">
        <button onClick={() => doRequest('account')}>Account</button>
        <button onClick={() => doRequest('wallet')}>Wallet</button>
        <button onClick={() => doRequest('bank')}>Bank</button>
        <button onClick={() => doRequest('materials')}>Materials</button>
        <button onClick={() => doRequest('characters')}>Characters</button>
        <button onClick={() => doRequest('commerce/prices')}>Commerce Prices</button>
      </section>

      <section className="generic">
        <input value={path} onChange={e => setPath(e.target.value)} placeholder="/v2 path" />
        <input value={query} onChange={e => setQuery(e.target.value)} placeholder="query" />
        <button onClick={genericGet}>GET</button>
      </section>

      <pre className="result">{result ? JSON.stringify(result, null, 2) : 'No results'}</pre>
    </div>
  );
}

export default App;
