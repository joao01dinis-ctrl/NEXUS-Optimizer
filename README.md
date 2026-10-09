# NEXUS Optimizer

Aplicação Windows x64 gratuita, em C# / .NET 10 / WinUI 3 / Windows App SDK. Sem conta NEXUS, subscrição ou telemetria própria. Código e apresentação próprios, licença MIT.

**0.6 experimental:** revisão do fluxo de utilização com cartões diretos, PC Boost, Game Mode automático e restauro global. Build e testes em [VALIDATION.md](VALIDATION.md); utilização em [USAGE.md](USAGE.md). Sem assinatura de distribuição; o arranque da 0.6 ainda requer confirmação. Não apresentar esta revisão como pronta neste PC nem como paridade integral com TL Optimizer.

## Funções implementadas no código

- Dashboard: identificação CPU/GPU/RAM/discos, utilização CPU/RAM, gráficos até 60 amostras e tempo desde o arranque. GPU identificada não significa utilização GPU medida.
- Análise local de pressão de CPU/RAM/espaço livre. Verificação dos últimos ajustes do NEXUS que diferem do valor guardado, com reaplicação confirmada; não assume que a mudança externa era um erro.
- Energia: seleção de planos existentes, captura anterior e undo. Não cria um plano que desativa core parking ou energia de USB/PCIe.
- Interface/teclado: animações de conteúdo e treze preferências por APIs Windows, com undo. Repetição de teclas não é redução da latência do primeiro toque.
- PC Boost: objetivos Normal/Gaming/Trabalho, análise de valores atuais, seleção de diferenças e aplicação em conjunto após revisão. Confere se o estado mudou e tenta repor as alterações desta aplicação se ocorrer falha, incluindo escritas interrompidas.
- Tweaks/Windows: cartões com estado atual e ligar/desligar. Desligar repõe o valor capturado; uma preferência já configurada sem histórico NEXUS não recebe um padrão inventado. Teclas de Aderência usa a API Windows e preserva os outros flags.
- Jogos e aplicações: biblioteca SQLite de executáveis locais escolhidos, deteção por caminho completo e identidade PID + arranque, aviso opcional de abertura. Não inicia executáveis. Automático opcional aplica apenas a lista/perfil autorizados nesta abertura; mudar biblioteca/configuração exige pausar e reativar. Sem repetição automática depois de falha ou de terminar manualmente a mesma instância.
- Perfis de sessão Balanceado/Competitivo/Extremo/Streaming/Personalizado, com prioridade Normal/AboveNormal, animações e plano disponível opcionais. Extremo mantém os mesmos limites seguros de Competitivo; são definições próprias. Reposição das alterações da sessão ao terminar o processo, enquanto a app está aberta; reposição da sessão ativa ao fechar normalmente a NEXUS e recuperação manual após reiniciar.
- App Tuner: prioridade AboveNormal de um programa aberto elegível, com valores anteriores no histórico. Exclui Windows, proteção, anti-cheat e launchers conhecidos; não altera ficheiros ou configurações internas do programa.
- Memória: processos acessíveis e memória residente/privada; redução opcional do working set de uma aplicação escolhida em segundo plano. Exclui componentes Windows, primeiro plano, o NEXUS, apps da biblioteca abertas e sessões ativas. Pode provocar mais leituras de disco e lentidão; não liberta memória privada nem tem undo. Não purga standby ou páginas modificadas do sistema.
- Rede: adaptadores, IPs, gateway, DNS e DHCP; comparação de quatro fornecedores por três consultas UDP/IPv4 de example.com, com respostas validadas, mediana, timeout e cancelamento. Não mede ping de jogos.
- Configuração DNS: interface física Ethernet/Wi-Fi elegível, DNS IPv4 estático ou automático via WMI, captura da configuração anterior e undo. Requer iniciar como administrador. Não gere VPNs/DoH/NRPT; pode interferir com redes internas. Renovação DHCP e limpeza da cache DNS são ações separadas, confirmadas, sem undo.
- Latência de rede: cinco pedidos ICMP ao IP introduzido, média, mínimo/máximo, perda e variação entre respostas. Não mede latência de entrada/FPS; ICMP bloqueado não prova falha da ligação.
- Benchmark: SHA-256 e cópia de memória numa tarefa, curto, cancelável, SQLite e comparação por método/ambiente. Não atribui a variação a ajustes nem mede o desempenho de jogos.
- Capturas de FPS: importa CSV por fotograma do PresentMon com MsBetweenPresents. Separa aplicação/PID/fluxo, mantém pausas, calcula FPS de apresentação/1% low/mediana/P99; guarda origem/hash/condições e recusa comparações incompatíveis ou insuficientes. Não captura jogos, executa PresentMon, injeta overlay ou mede FPS efetivo no ecrã/latência de entrada. [Método e limites](FPS-VALIDATION.md).
- CPU por aplicação: duas amostras durante cerca de dois segundos, normalizadas pelo tempo e CPUs lógicas, com identificação PID/arranque. Mostra processos acessíveis da sessão; não altera prioridades ou termina Discord/navegador/gravação.
- FiveM: guia de diagnóstico com comandos oficiais para consultar métricas, atalhos para comparar capturas e consultar CPU. Não executa comandos no jogo nem apresenta um resultado de otimização sem medições. [Como validar no FiveM](FIVEM-VALIDATION.md).
- Limpeza manual: TEMP, cache DirectX, cache web Epic e atalhos Recent quando as pastas existem. Só ficheiros sem escrita/acesso há 7 dias, com seleção/revalidação, limites e envio forçado à Lixeira. Não segue ligações nem remove diretórios. Limpar shaders pode causar recompilação/pausas e cache web pode exigir novo login.
- Auto limpeza opcional: só a pasta local Temp do utilizador, desligada em cada arranque, intervalos 15/30/60 minutos, idade 30 dias, máximo 100 ficheiros/10 MiB por ficheiro/256 MiB por execução. Suspende durante sessões/apps da biblioteca detetadas. Envia à Lixeira, não a esvazia; pode afetar jogos não identificados. Não é um serviço permanente.
- Windows/privacidade: sete políticas documentadas do utilizador para experiências personalizadas, sugestões/Spotlight e personalização Edge. Compatibilidade por edição; recusa política do computador e tipos inesperados. Guarda ausência ou DWORD anterior, com undo. Gravar a política não comprova o efeito no Windows/conta. Não desliga todo o diagnóstico do Edge.
- Aplicações nativas: lista de 28 pacotes opcionais conhecidos, consulta, remoção individual confirmada para o utilizador atual, revalidação da identidade antes da ação. Exclui Store/segurança/shell/frameworks; o Windows pode recusar pacotes não removíveis. Desinstalação pode eliminar dados e não tem undo. Reinstalação abre pesquisa na Store e depende da disponibilidade/conta; não é restauração automática dos dados.
- Histórico: alterações reversíveis, sessões, benchmarks/capturas FPS e eventos de memória/rede/limpeza/remoção em SQLite; exportação consistente sem substituir ficheiros existentes. Pesquisa de páginas e catálogo pesquisável com 138 entradas da referência e duas ferramentas próprias de verificação.

As funções implementadas são gratuitas. Ganhos dependem do hardware/carga; não existem números de FPS fabricados. SmartScreen, Defender, firewall, atualizações, recuperação, serviços essenciais, drivers e paginação são preservados.

## Executar e desenvolver

Usar a publicação completa, não apenas o EXE. `Nexus.UI.exe` está em `artifacts/app-v0.6-final` para esta revisão. `Start-Nexus.cmd` mantém a publicação anterior até confirmar o arranque da nova revisão; a pasta 0.6 inclui um iniciador próprio. A versão local permanece sem assinatura e pode ser bloqueada por políticas do Windows, mesmo tendo aberto neste PC.

```powershell
./build.ps1
./build.ps1 -Publish
```

SDK .NET 10.0.401 ou patch compatível; Windows 10 2004+ / Windows 11 x64. `-DotnetPath` permite indicar outro SDK. NuGet/Windows App SDK são incluídos no publish. Microsoft.Data.Sqlite 10.0.12 e SQLite nativo 3.50.3; licenças em [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

A app abre sem elevação. Ações que exigem administrador recusam operar sem esse direito; não se eleva nem altera políticas de segurança automaticamente. Políticas bloqueadas, adaptadores incompatíveis e jogos protegidos mostram erro.

## Dados e restauro

`%LOCALAPPDATA%/NexusOptimizer/nexus.db` guarda perfis, biblioteca, alterações, sessões, medições e eventos; `logs` contém os logs. Não apagar a base de dados/desinstalar enquanto existirem ajustes por repor. A exportação inclui todas as tabelas, não logs, e ainda não tem importação automática.

Antes de cada alteração reversível, o journal grava a intenção e o valor anterior. Verifica escrita/reposição e recusa substituir mudanças externas. Undo é por ordem inversa e não substitui um ponto de restauro do Windows. Sessões só repõem alterações que lhes pertencem. O fecho normal tenta repor a sessão ativa e recusa fechar enquanto a recuperação falha. Alterações permanentes de energia/animações/DNS/políticas permanecem até usar Restaurar alterações ou Histórico. O restauro global preserva conflitos externos e tenta recuperar outras definições independentes. Limpeza usa a Lixeira; cache DNS, concessão IP, working sets e desinstalação não têm undo.

`NEXUS_DATA_DIR` isola dados de testes. `--smoke-test` tenta abrir, capturar imagem/métricas e fechar, sem aplicar ajustes. Em falha de arranque guarda `startup-error.txt` e sai com erro. `NEXUS_SCREENSHOT_PAGE` aceita overview, optimize, gaming, memory, network, benchmark, tools, history, preferences, apps, latency, tweaks, tuner, autoclean, help ou catalog. Não importar certificados nem desligar proteções para executar a revisão.

## Cobertura da referência

[FEATURES.md](FEATURES.md) e [TL-PARITY.md](TL-PARITY.md) distinguem código implementado, atalhos, pendências e exclusões. O catálogo completo é embebido em Core e consumido pela UI, para manter uma fonte de estado para trabalho futuro. Cada pendência necessita especificação, compatibilidade, efeito, reversão, executor e validação; uma entrada não é uma função executável.

Continuam ausentes captura automática/overlay de FPS, benchmark integrado de jogos, latência de entrada, motor adaptativo de otimização, importação de bibliotecas de launchers, App Tuner específico por produto, políticas de Office/Firefox/Chrome/NVIDIA/Visual Studio, gestão de serviços e parte das preferências Windows. Os ajustes Premium desfocados, onze opções avançadas de Game Mode, três opções Toque e parte inferior da grelha precisam de identificação. [A revisão das descrições TikTok](TIKTOK-REVIEW.md) regista também os erros de reprodução, sem fingir que todos os vídeos foram vistos. Opções agressivas/conflituantes não são implementadas como otimização. Não foi obtido código ou acesso pago ao TL.

Módulos: UI, Core, Hardware, Optimization, Gaming, Diagnostics, Benchmark, Network, Storage, Restore, Service e Tests. A monitorização é interna; não instala serviço Windows. Assinatura/distribuição: [CODE_SIGNING.md](CODE_SIGNING.md). Privacidade: [PRIVACY.md](PRIVACY.md).
