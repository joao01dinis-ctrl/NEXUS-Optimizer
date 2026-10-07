# NEXUS Optimizer

Aplicação Windows x64 gratuita, em C# / .NET 10 / WinUI 3, sem conta, subscrição ou telemetria. Licença MIT. Não é uma cópia nem uma versão desbloqueada do TL Optimizer.

Estado da 0.2: compilação concluída e 57 testes aprovados em Debug. Continua experimental e sem assinatura de distribuição; o arranque neste PC foi bloqueado pelo Controlo de Aplicações do Windows. Não está validada como pronta a usar. Resultados e limites em [VALIDATION.md](VALIDATION.md).

## Versão 0.2 — funções

- Dashboard com CPU/RAM e identificação de CPU, GPU e volumes.
- Análise local de pressão de CPU/RAM e espaço livre; não mede FPS nem promete ganhos.
- Seleção e aplicação de planos de energia já existentes no PC.
- Redução de animações de conteúdo do Windows por API oficial.
- Oito preferências opcionais de interface/teclado por API oficial: menus, dicas, listas de escolha, deslocação de listas, seleção, minimizar, arrastar janelas e repetição do teclado. Guardam os valores anteriores; repetição do teclado não é latência de entrada.
- Prioridade AboveNormal de uma aplicação aberta escolhida pelo utilizador, limitada à sessão do processo. Não usa RealTime/High, não termina processos e não altera afinidade.
- Sessão de jogo com prioridade e plano de energia opcionais, histórico persistente, reposição ao terminar o processo enquanto a app está aberta e recuperação manual após reiniciar. A sessão só repõe alterações que lhe pertencem e verifica alterações externas antes de escrever.
- Lista de processos acessíveis da sessão atual, ordenados pela RAM residente, com memória privada. Não esvazia working sets, standby ou páginas modificadas.
- Comparação DNS de Cloudflare, Google, Quad9 e AdGuard: três consultas UDP/IPv4 de example.com, mediana das respostas válidas, timeout e cancelamento. Não altera adaptadores, DNS ou TCP; não mede ping de jogos.
- Revisão de ficheiros da pasta temporária do utilizador sem escrita/acesso há pelo menos 7 dias. Seleção manual, revalidação e envio forçado à Lixeira, sem eliminação permanente alternativa. Não segue reparse points nem remove pastas. Limites de 2000 ficheiros examinados/10 segundos; a análise pode ser parcial. A Lixeira continua a ocupar espaço até ser esvaziada pelo utilizador, e é o mecanismo de restauro desta ação.
- Confirmação antes de aplicar e histórico SQLite com estado anterior guardado antes da escrita. Undo individual, por ordem inversa; recusa sobrescrever alterações externas. Operações interrompidas ficam disponíveis para recuperação.
- Atalhos identificados para ferramentas oficiais: armazenamento, aplicações de arranque, Modo de Jogo, GPU, capturas, acessibilidade, personalização, privacidade, Gestor de Tarefas, otimização de unidades, restauro, rede e Windows Update. Alterações feitas nessas ferramentas não são desfeitas pelo NEXUS.
- Perfis antigos Normal/Gaming/Trabalho preservados como ritmo do dashboard, separados das otimizações reais.

Os planos podem aumentar calor, consumo e ruído; a prioridade pode prejudicar outras aplicações. Ganhos dependem do hardware, jogo e carga. Não são alteradas proteções do Windows, serviços, drivers ou ficheiros pessoais automaticamente. A app abre sem elevação; opções bloqueadas por políticas ou jogos protegidos mostram um erro.

## Executar

Usar a publicação completa e abrir `Nexus.UI.exe`. Não copiar apenas o EXE. A publicação local da 0.2 fica em `artifacts/app-v0.2`; `Start-Nexus.cmd` prefere essa pasta e usa `artifacts/app` como alternativa.

```powershell
./build.ps1
./build.ps1 -Publish
```

SDK .NET 10.0.401 ou patch compatível; Windows 10 2004+ / Windows 11 x64. `-DotnetPath` permite indicar outro executável dotnet. As dependências vêm de NuGet e o publish inclui os runtimes. Microsoft.Data.Sqlite 10.0.12 com SQLite nativo 3.50.3 corrigido; veja THIRD_PARTY_NOTICES.md.

## Dados e restauro

`%LOCALAPPDATA%/NexusOptimizer/nexus.db` guarda perfis e alterações do sistema; logs na subpasta `logs`. Não eliminar a base de dados enquanto existirem ajustes a repor. Fechar a app não repõe energia/animações automaticamente: usar Histórico. Prioridade termina com o processo. A identidade de um processo inclui a hora de arranque para não atingir outro com o mesmo PID.

O journal regista pending antes da ação, applied depois de verificar, reverted após reposição e failed quando a falha não mudou o estado. Um mutex por base de dados serializa as operações entre instâncias. Se uma operação falhar a meio, as anteriores permanecem aplicadas e disponíveis no histórico. O undo não é um ponto de restauro completo do Windows. Sessões antigas são apresentadas para recuperação manual; abrir a app não repete ajustes interrompidos. Se o NEXUS fechar durante uma sessão, o plano pode continuar aplicado até Terminar e repor.

## Validação e assinatura

A versão 0.1 abriu e atualizou métricas neste PC em 7 de outubro de 2026, sem alterar proteções. O bloqueio de Controlo de Aplicações observado em setembro não se reproduziu; a causa dessa mudança não foi determinada. A app continua sem assinatura de distribuição e pode ser bloqueada noutros ambientes.

A candidatura SignPath foi recusada por falta de reconhecimento público. [Code signing policy](CODE_SIGNING.md), [privacidade](PRIVACY.md), [dependências](THIRD_PARTY_NOTICES.md).

`NEXUS_DATA_DIR` permite dados isolados. `--smoke-test` testa o arranque, guarda imagem e métricas e fecha sem aplicar ajustes. `NEXUS_SCREENSHOT_PAGE` permite capturar overview, optimize, gaming, memory, network, tools ou history durante esse teste. Consulte VALIDATION.md para distinguir compilação, testes e arranque realmente verificados nesta versão.

## Organização

UI, Core, Hardware, Optimization, Gaming, Diagnostics, Benchmark, Network, Storage, Restore, Service, Tests. O serviço de monitorização corre dentro da app. FPS, benchmarks de jogos, deteção automática de bibliotecas, motor adaptativo, latência de entrada e o catálogo completo de ajustes do TL não estão implementados. Consulte [FEATURES.md](FEATURES.md) e o inventário do vídeo em [VIDEO-FEATURES.md](VIDEO-FEATURES.md). A 0.2 não tem paridade integral com o produto de referência.
