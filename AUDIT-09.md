# Auditoria e entrega incremental — NEXUS 0.9

Pedido recebido em 15 de outubro de 2026. Foi preservada a solução C#/WinUI existente e os módulos UI, Core, Hardware, Optimization, Gaming, Diagnostics, Benchmark, Network, Storage, Restore, Service e Tests. Não há nova aplicação ou substituição por uma demonstração.

| Área | Estado encontrado na 0.8 | Alteração nesta revisão / limite |
|---|---|---|
| Arranque | Smart App Control bloqueava DLLs sem assinatura neste PC | A 0.9 abriu neste portátil com Smart App Control ativo e cinco páginas capturadas, sem mudar políticas. A submissão Store 0.7 permanece independente |
| Interface | Cinco áreas principais, pesquisa, temas e modo leve | Início, Otimização, Os meus jogos, Game Boost, Rato e Histórico; hardware/GPU e VPN em Mais ferramentas; favoritos e guia opcional |
| Botão principal | Podia aplicar preferências locais elegíveis depois de analisar | Otimizar o meu PC abre análise e propostas; o utilizador confirma a aplicação separadamente |
| Jogos instalados | Seleção manual do EXE | Descoberta de metadados: Steam em vários discos, Epic, Riot, Xbox/Store acessíveis, registos EA/Ubisoft/Battle.net, FiveM, Roblox e Minecraft Java |
| Jogos em execução | Caminhos escolhidos manualmente | Integra instalações descobertas; FiveM aceita o padrão do processo GTA apenas dentro da raiz autorizada |
| Perfis | Perfil comum da sessão atual | SQLite guarda escolhas por instalação; Padrão/Competitivo/Máximo e perfis personalizados exportáveis/importáveis |
| Game Boost | Sessões com diário e recuperação | Perfis por jogo congelados ao ativar; novos jogos ou perfis editados não ampliam a autorização |
| Rato | Aceleração e cálculo de sensibilidade | Velocidade de 1 a 20 com captura do valor anterior, verificação e Undo; DPI/polling/USB não são alterados |
| CPU/energia | Hardware e planos Windows existentes, reversíveis | Preservados. Energia não é alterada pelos novos perfis; alimentação continua a condicionar sessões avançadas |
| GPU | Nome/driver e orientações | Página de hardware real e orientações por jogo; não escreve perfis globais NVIDIA |
| Monitorização | CPU/RAM, discos, gráficos e importação PresentMon | Preservada; temperaturas, utilização/VRAM/clocks GPU, atividade de disco e captura FPS integrada não estão implementadas |
| Rede | Adaptadores, DNS, ICMP, perda e jitter | Preservada; diagnóstico separado da VPN e sem confundir ICMP com ping do servidor do jogo |
| Serviços/privacidade | Políticas de utilizador e ferramentas Windows | Preservadas; sem nova desativação genérica de serviços, energia USB ou proteções |
| Limpeza | Revisão de temporários, Lixeira e apps opcionais individuais | Preservados os limites e confirmações; sem eliminação em massa de dados/componentes essenciais |
| Recuperação | SQLite, estado anterior, CAS, sessões e Undo | Estendida aos cinco efeitos visuais por sessão e à velocidade do ponteiro |
| Nexos VPN | Ausente | Estudo e página de indisponibilidade; pré-requisitos e seleção testáveis. Sem servidor, túnel, proteção ou IP anunciados como reais |

## Descoberta e limites

A abertura inicia uma procura. Os meus jogos permite repetir e, enquanto estiver visível, atualiza no máximo a cada cinco minutos. A procura de processos autorizada é limitada a uma vez por cinco segundos. Operações e sessões suspendem a pesquisa de instalações.

Não se percorre um disco inteiro, segue junctions, toma posse de WindowsApps, lê outra conta ou arranca um jogo. Metadados têm limite de 2 MiB e cada diretório consultado tem limite de 512 entradas. Os parsers recusam executáveis fora da instalação, fluxos NTFS e entidades XML externas. O cache anterior é preservado quando uma fonte está indisponível.

**Não se garante deteção de 100% dos jogos.** Os formatos podem mudar e as instalações podem estar protegidas ou não registadas. EA/Battle.net usam registos acessíveis; não há parser do banco binário privado product.db. Ubisoft usa registos de instalação e identifica o EXE quando os metadados o permitem. Minecraft Java é identificado pelos ficheiros de versões; um processo Java partilhado não é autorizado só pelo nome. A interface mostra o limite e mantém o recurso manual avançado.

Um jogo não é rotulado como otimizado até existir uma sessão correspondente. Um manifesto sem EXE acessível não habilita aplicação automática.

## Perfis e segurança

- Padrão acompanha a sessão sem alterar o Windows.
- Competitivo permite reduzir animações de conteúdo e menus.
- Máximo permite cinco preferências visuais listadas. Não é overclock nem promessa de FPS.
- O perfil personalizado usa as preferências de um modelo com nome próprio. Pode ser editado guardando o mesmo nome; a importação só aceita nome e chaves visuais permitidas.
- Escolhas e perfis persistem. Executar automaticamente exige confirmação por abertura e fixa jogos/valores. Editar o catálogo não modifica a autorização ativa.
- Cada sessão captura valores reais, verifica escrita, repõe ao sair e mantém recuperação após falhas. Os novos perfis não alteram prioridades, energia, ficheiros, segurança ou VPN.

## Pendências explícitas

1. Distribuição assinada e arranque na torre. O arranque da 0.9 foi confirmado neste portátil, sem assinatura; o hardware fornecido para a torre não é uma medição do portátil.
2. Testes com bibliotecas reais de cada launcher; fixtures não são jogos reais do PC.
3. Medições repetidas no mesmo jogo/cenário para demonstrar eventuais ganhos. Nenhum ganho FiveM/CS2 foi demonstrado.
4. Sensores/GPU, captura FPS integrada, perfis gráficos por jogo e diagnóstico mais amplo.
5. Serviços opcionais exigem análise individual; não se justifica desativá-los sem benefício demonstrado.
6. VPN funcional: orçamento, infraestrutura, componentes assinados, integração administrativa e testes de fugas/recuperação. [Estudo e orçamento](NEXOS-VPN-STUDY.md).

Ver [utilização e validação](VALIDATION-09.md). O catálogo histórico TL/PrecisionFix foi preservado; não se anuncia equivalência completa.
