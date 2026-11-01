# Koichi Boost v0.2 — Otimizador para VALORANT e baixa latência de entrada

Programa para Windows 10/11 x64. **Abre com duplo clique** num menu interativo (ou por linha de comando).
Não exige administrador. Nada é alterado sem a sua confirmação, e tudo que é alterado pode ser desfeito.

## Como gerar o executável (.exe)
**A) No seu PC** (precisa do [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)):
```powershell
cd agent
.\publish.ps1          # gera agent\dist\koichi-agent.exe (autocontido)
```
**B) Sem instalar nada:** suba esta pasta para o GitHub; em *Actions* rode `build-windows-exe` e baixe o `koichi-agent.exe`.

> O .exe não é assinado digitalmente: o SmartScreen/Defender pode avisar na primeira execução.
> Este código ainda não foi compilado por mim (ambiente sem .NET). Se o build der erro, envie a saída completa.

## Usando
Dê duplo clique em `koichi-agent.exe`:

| Menu | O que faz |
|---|---|
| 1) OTIMIZAR PARA VALORANT | Analisa, mostra o plano (por quê, o que muda, risco), pede confirmação, faz backup, aplica e **verifica** |
| 2) ANALISAR MEU PC | Diagnóstico completo + relatório em `Documentos\KoichiBoost\reports` |
| 3) RESTAURAR CONFIGURAÇÕES | Devolve cada ajuste ao valor original |
| 4) Fechar apps de segundo plano | Fecha (graciosamente, só o que você escolher) navegador, launchers, overlays |
| 5) Configurações no VALORANT | Recomendações dentro do jogo e avisos de anti-cheat |

Linha de comando: `koichi-agent optimize`, `diagnose`, `restore --last`, `close-apps`, `tips` (veja `--help`).
Backups ficam em `%LOCALAPPDATA%\KoichiBoost\backups`.

## O que ele ajusta (somente se o problema for detectado no SEU PC)
| Ajuste | Para quê | Risco |
|---|---|---|
| Plano de energia "Economia" → Alto desempenho/Equilibrado | Frequências de CPU sem limite artificial | Baixo |
| VALORANT na GPU de alto desempenho (corrige "economia" ou fixa com iGPU+dGPU) | Garantir RX 5700 XT em vez da iGPU | Baixo |
| Modo de Jogo ligado | Priorização do jogo pelo Windows | Baixo |
| Gravação em segundo plano (Game DVR) desligada | Menos carga de captura | Baixo |
| Atalhos de acessibilidade (Shift 5x…) desligados | Evita pop-up que tira o foco do jogo | Baixo |
| Aceleração do mouse desligada *(opcional)* | Mira consistente | Baixo |
| Itens de inicialização conhecidos desativados *(opcional)* | Menos RAM/CPU ocupados | Baixo |

Só diagnostica (sem alterar): monitor a 60 Hz, RAM em canal único/sem XMP, drivers, Secure Boot/TPM (Vanguard), temperaturas (opt-in), espaço em disco, V-Sync/limite de FPS no jogo.

## O que NÃO faz, de propósito
Desativar Defender/firewall/Vanguard/serviços, overclock, mexer na BIOS, injetar código, alterar arquivos do jogo. Também **não** aplica HAGS, "otimizações de tela cheia", timer resolution, MPO ou tweaks de TCP: efeito inconsistente ou não comprovado. Só entrariam após teste A/B no seu PC.

## Sobre "remover input lag"
Não existe ajuste de software que "remova" input lag. O app elimina causas reais e mensuráveis (GPU errada, plano de energia limitado, pop-ups, V-Sync, 60 Hz no monitor, aceleração do mouse). Não promete ganho de FPS nem de ms. A medição de latência/FPS antes/depois (benchmark) é o próximo módulo.

## Desenvolvimento
`cd agent; .\build.ps1` compila e roda os testes (Core roda em qualquer SO). Arquitetura: `docs/ARCHITECTURE.md`.
