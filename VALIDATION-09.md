# Utilização e validação — NEXUS 0.9

Revisão de desenvolvimento da aplicação existente. Build local concluído sem avisos nem erros. Suite local: **201 aprovados, 5 ignorados, 0 falhados**; relatório em artifacts/tests-v0.9-local/tests-09-final.trx. Quatro testes nativos de preferências e um teste de integração da Lixeira não foram executados na suite normal. Os testes que alteram definições reais são limitados ao runner descartável do GitHub.

Em 15 de outubro de 2026, a publicação completa 0.9 abriu neste portátil e produziu uma captura nativa sem erro de arranque, com Smart App Control ativo (estado 1). O ensaio apenas leu hardware e instalações: **0 alterações no Windows**, sem sessão de jogo, sem automação e sem envio à OpenAI. A biblioteca encontrou 0 jogos neste portátil; isto não valida a deteção na torre do utilizador. A versão continua experimental e sem assinatura; a submissão Store 0.7 permanece independente. Os testes de escrita/restauro da revisão 0.9 no runner ainda aguardam execução.

## Utilização

1. Usar uma distribuição reconhecida pelo Windows. A publicação WinUI precisa de todas as dependências, XBF e PRI, não apenas o EXE. A submissão Store 0.7 é independente desta revisão.
2. Em Início, usar **Otimizar o meu PC**, aguardar cinco amostras, rever propostas e confirmar só as alterações escolhidas.
3. Em **Os meus jogos**, aguardar a descoberta ou atualizar. Não é necessário escolher EXE nas instalações reconhecidas. Ver limites em AUDIT-09.md.
4. Escolher perfil no cartão, abrir o jogo normalmente, usar **Otimizar este jogo**, rever e confirmar.
5. Para automático, escolher o opt-in de cada jogo e usar **Ativar / pausar Game Boost dos meus jogos**. A confirmação fixa os jogos/valores desta abertura. Manter a NEXUS aberta para acompanhar o fim do jogo.
6. **Repor sessão** e **Histórico** recuperam valores guardados. Alterações externas podem exigir revisão para não sobrescrever escolhas feitas fora da aplicação.
7. Nexos VPN permanece indisponível. Não é uma proteção de rede nesta revisão.

## Cobertura verificada pela suite local

- Steam com biblioteca secundária, Epic, Riot, Xbox/Store e registos fornecidos de launcher.
- Metadados inválidos, entidades XML externas, cancelamento e executáveis fora da instalação.
- Persistência SQLite sem escrita no Windows, importação limitada, autorização imutável por jogo e restauro de todos os valores visuais com definições simuladas.
- Velocidade do rato: validação de limites; escrita/restauro real é opt-in no Windows descartável.
- VPN: apenas pré-requisitos e seleção conservadora; nenhum túnel, medição de IP ou teste de fugas.

O arranque local foi confirmado separadamente do build e dos testes. Não há ganhos em jogos demonstrados, deteção real validada na torre, garantia de todos os jogos ou VPN operacional. Fixtures de cartões e metadados são sempre identificadas como dados de teste e só são criadas pelo modo de teste no runner descartável.
