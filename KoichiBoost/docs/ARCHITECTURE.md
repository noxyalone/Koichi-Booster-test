# Koichi Boost — Arquitetura

## Princípios (valem para todas as fases)

1. **Medir antes de mexer.** Toda otimização precisa de evidência no hardware detectado e, depois, de validação A/B. Sem melhora mensurável, o app diz isso.
2. **Reversível ou não entra.** Cada alteração tem backup do valor anterior e rotina de restauração.
3. **Catálogo fechado de ações.** O agente só executa ações que existem *compiladas nele*, identificadas por ID com parâmetros tipados. Nunca executa scripts recebidos da nuvem.
4. **Nada contra segurança/anti-cheat.** Sem desativar Defender, firewall, Vanguard, serviços essenciais; sem injeção de código; sem overclock/BIOS automáticos.
5. **Consentimento local.** Qualquer mudança é confirmada no próprio PC (e UAC quando exigir administrador). O agente nunca se auto-eleva.
6. **Honestidade nos números.** `n/d` quando não foi possível medir. Nenhum ganho de FPS é estimado ou prometido.

## Visão geral

```
┌────────────────────┐   HTTPS (REST)    ┌──────────────────────────┐
│ Frontend (React+TS)│ ────────────────► │ Backend (FastAPI)        │
│ painel preto/roxo  │ ◄──────────────── │ perfis, regras, relatórios│
└────────────────────┘                   │ PostgreSQL · auth · premium│
                                         └───────────▲──────────────┘
                                                     │ WSS saída (agente → nuvem)
                                                     │ token de dispositivo + pacotes de regras assinados
                                         ┌───────────┴──────────────┐
                                         │ Agente Windows (C#/.NET 8)│
                                         │  Collectors (somente leitura)
                                         │  Rules (Core, multiplataforma)
                                         │  [fase 2] Actions + Backup/Restore
                                         └──────────────────────────┘
```

- **Agente → nuvem, sempre conexão de saída.** Sem porta aberta no PC. Pareamento por código de uso único exibido no agente e digitado no site.
- **Envio de relatórios é opt-in.** O agente funciona 100% offline (gera `.md` e `.json` locais).
- **Pacotes de regras/perfis** vindos do backend são assinados (Ed25519); o agente verifica a assinatura. Um backend comprometido não consegue mandar comandos arbitrários, apenas parâmetros de ações do catálogo, que ainda exigem confirmação local.
- **Premium (futuro):** perfis por hardware, histórico de benchmarks na nuvem, comparação entre sessões. Ficam no backend, sem afetar o núcleo gratuito/local.

## Estrutura de pastas

```
KoichiBoost/
├─ docs/ARCHITECTURE.md
├─ agent/                          # Agente Windows (esta entrega)
│  ├─ build.ps1
│  ├─ src/
│  │  ├─ KoichiBoost.Core/         # net8.0 — modelos, regras, relatórios (testável em qualquer SO)
│  │  │  ├─ Models/                # DiagnosticSnapshot, Finding
│  │  │  ├─ Rules/                 # uma regra = um problema verificável
│  │  │  └─ Reporting/             # JSON + Markdown
│  │  ├─ KoichiBoost.Windows/      # net8.0-windows — coletores (WMI, registro, P/Invoke de leitura)
│  │  └─ KoichiBoost.Agent/        # CLI (koichi-agent.exe)
│  └─ tests/KoichiBoost.Core.Tests/
├─ backend/                        # FastAPI (fase 3)
└─ frontend/                       # React + TypeScript (fase 3)
```

Separação intencional: **Core não conhece o Windows**. Regras são funções puras sobre um `DiagnosticSnapshot`, então são testadas com dados fabricados (inclusive de outros PCs) e poderão rodar também no backend para reanálise.

## Fluxo do diagnóstico (v0.1)

`SnapshotBuilder` → roda coletores isolados (falha de um vira aviso) → `RuleEngine` avalia regras → `DiagnosticReport` → `.md` + `.json`.

### O que o agente lê (e como)

| Dado | Fonte | Escrita? |
|---|---|---|
| Windows, build | Registro HKLM (leitura), `Environment` | não |
| Placa-mãe, BIOS, CPU, GPU, RAM, discos | WMI (`Win32_*`, `MSFT_PhysicalDisk`) | não |
| Uso de CPU/RAM | `GetSystemTimes`, `GlobalMemoryStatusEx`, `Process` | não |
| Plano de energia | `powercfg /getactivescheme` | não |
| Game Mode, Game DVR, HAGS, GPU preference, compat flags | Registro (`writable: false`) | não |
| Secure Boot, TPM | Registro / WMI `Win32_Tpm` | não |
| Vanguard (`vgc`, `vgk`) | `ServiceController` (estado) | não |
| Config local do Valorant | `GameUserSettings.ini` (aberto como leitura compartilhada) | não |
| Temperaturas (opt-in) | LibreHardwareMonitorLib | carrega driver temporário (ver riscos) |

## Riscos conhecidos desta fase

- **Sensores × Vanguard.** Ler temperatura de CPU exige driver de kernel de terceiros. Alguns drivers desse tipo são bloqueados pelo Vanguard, por isso `--sensors` é opt-in, o driver é descarregado ao final e recomenda-se rodar com o Valorant fechado. Alternativa futura: leituras via APIs do fabricante (AMD ADLX) sem driver próprio.
- **Config do Valorant.** Parte das configurações fica na nuvem da Riot; o `.ini` local é lido em modo *best-effort* e os achados vêm marcados como *inferido*.
- **Throttling.** Temperaturas altas sugerem risco, mas a v0.1 não mede throttling diretamente.
- **Duas GPUs.** O Ryzen 5 5600GT tem iGPU Vega; o app detecta se o monitor está na iGPU ou se o Windows prefere a GPU de economia para o Valorant — causa clássica de FPS baixo em PCs com iGPU + placa dedicada.

## Roadmap

| Fase | Entrega | Status |
|---|---|---|
| 0 | Agente de diagnóstico somente leitura + relatório Valorant | **esta entrega** |
| 1 | Benchmark: captura de frametimes (PresentMon/ETW, sem driver), FPS médio, 1% lows, comparação antes/depois | próxima |
| 2 | Backup/restauração + ações reversíveis (plano de energia, GPU preference, Game DVR, Game Mode, itens de inicialização), cada uma validada por A/B | — |
| 3 | Backend FastAPI + frontend React (tema preto/roxo), pareamento, relatórios na nuvem | — |
| 4 | Premium: perfis por hardware, histórico, regras assinadas | — |

### Catálogo de ações planejado (fase 2) — finalidade e risco

| Ação | Finalidade | Risco | Reversão |
|---|---|---|---|
| Plano de energia | Evitar plano "Economia" | Baixo | Reativar GUID anterior |
| Preferência de GPU do Valorant → Alto desempenho | Garantir uso da GPU dedicada | Baixo | Restaurar valor anterior da chave |
| Desativar gravação em segundo plano (Game DVR) | Reduzir carga de captura | Baixo | Restaurar chave |
| Ativar Modo de Jogo | Priorização do jogo pelo Windows | Baixo | Restaurar chave |
| Desativar itens de inicialização selecionados | Menos RAM/CPU ocupados | Baixo | Reabilitar item (StartupApproved) |
| HAGS, FSO, V-Sync etc. | **Somente após A/B mostrar ganho** no seu PC | Médio | Restaurar chave |

Fora do escopo, sempre: Defender, firewall, Vanguard, serviços essenciais, overclock, BIOS.
