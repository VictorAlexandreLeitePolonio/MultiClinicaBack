# Tipos de sessão por clínica

## Objetivo

Substituir o enum fixo de tipos de sessão por um catálogo persistido e isolado por clínica. Administradores poderão cadastrar um tipo durante o cadastro ou a edição de um plano. O novo tipo ficará disponível imediatamente no campo pesquisável sem apagar os demais dados do formulário.

## Escopo

- Implementar backend e frontend.
- Permitir somente consulta e cadastro de tipos de sessão.
- Não implementar edição, inativação ou exclusão.
- Não criar página ou tabela de gestão.
- Manter `TipoPlano` e os fluxos financeiros inalterados.

## Persistência e migração

Criar a tabela `SessionTypes` com:

- `Id`: chave primária inteira.
- `Name`: nome obrigatório, com até 100 caracteres.
- `ClinicaId`: chave estrangeira obrigatória para `Clinicas`.

O banco deve impedir nomes duplicados dentro da mesma clínica, ignorando maiúsculas, minúsculas e espaços nas extremidades. Clínicas diferentes podem usar o mesmo nome.

Substituir `Plans.TipoSessao` por `Plans.TipoSessaoId`, uma chave estrangeira obrigatória para `SessionTypes`. A aplicação deve validar que o tipo informado pertence à clínica autenticada.

A migration fará o corte direto:

1. Criará `SessionTypes` e a coluna temporariamente anulável `Plans.TipoSessaoId`.
2. Inserirá para a clínica 2: Fisioterapia, Pilates, Massagem, Hidrolipo, Lipedema e Linfedema.
3. Relacionará os planos da clínica 2 pelo mapeamento explícito do enum legado: `0` a `5`, na ordem acima.
4. Inserirá para a clínica 3:
   - Audiometria Ocupacional
   - Avaliação/Anamnese
   - Consulta
   - Exame Audiometria e Impedanciometria
   - Exame de Audiometria
   - Exame de Audiometria e PAC
   - Exame Impedanciometria
   - Exame Processamento Auditivo Central (PAC)
   - Retorno
   - Retorno de Teste AASI
   - Terapia Fonoaudiológica
   - Terapia Ocupacional
   - Teste de AASI
   - Treinamento auditivo (PAC)
5. Tornará `Plans.TipoSessaoId` obrigatório, criará a FK e removerá a coluna enum.

A clínica 3 não possui planos. Nenhuma outra clínica receberá opções automaticamente.

## API de tipos de sessão

### Consulta

`GET /api/session-types?search=`

Retorna todos os resultados da clínica autenticada, em ordem alfabética, sem paginação. `search` é opcional e filtra o nome sem diferenciar maiúsculas de minúsculas.

```json
[
  { "id": 12, "name": "Consulta" }
]
```

### Cadastro

`POST /api/session-types`

```json
{ "name": "Acupuntura" }
```

O backend aplica `Trim`, exige de 1 a 100 caracteres e rejeita duplicidade normalizada na clínica. A clínica vem exclusivamente do usuário autenticado. Somente administradores podem consultar e cadastrar, seguindo a permissão atual de Planos.

O controller permanece fino. Service e repository concentram validação, isolamento tenant e persistência conforme o padrão existente.

## Contrato de Planos

Cadastro, edição e filtro passam a enviar `tipoSessaoId`.

```json
{
  "name": "Plano exemplo",
  "valor": 100,
  "tipoPlano": "Mensal",
  "tipoSessaoId": 12
}
```

As respostas retornam o identificador e o nome:

```json
{
  "tipoSessaoId": 12,
  "tipoSessaoName": "Consulta"
}
```

Criação, edição e filtro rejeitam tipos inexistentes ou pertencentes a outra clínica. Consultas de planos carregam a relação para preencher `tipoSessaoName`.

## Frontend

Criar service e hooks próprios para consultar e cadastrar tipos. O frontend não fará chamadas HTTP diretamente nos componentes.

Nos formulários de cadastro e edição:

- substituir as seis opções fixas por dados da API;
- exigir um `tipoSessaoId` válido, sem seleção padrão;
- oferecer pesquisa por nome;
- mostrar a lista em uma área rolável;
- mostrar carregamento, erro e ausência de resultados;
- exibir o botão `Cadastrar tipo de sessão` junto ao campo.

O botão abre um modal com apenas o nome. Após sucesso, o frontend atualiza o cache, fecha o modal e seleciona o novo registro. O formulário de plano permanece montado para preservar os campos preenchidos.

A listagem e os detalhes dos planos exibem `tipoSessaoName`. Tipos TypeScript, schema Zod, services e hooks passam a usar o novo contrato.

## Erros e segurança

- O backend nunca aceita `ClinicaId` do cliente.
- Busca, duplicidade e associação com planos sempre usam `usuario.ClinicaId`.
- Um ID de outra clínica produz a mesma resposta de tipo inválido ou não encontrado, sem revelar sua existência.
- A constraint do banco protege duplicidades concorrentes; o service retorna uma mensagem de domínio legível.
- Falhas no cadastro rápido mantêm o modal e o formulário abertos.

## Validação

Backend:

- testes do service/repository para isolamento, busca, normalização e duplicidade;
- testes da criação e edição de planos com tipo válido, inexistente e de outra clínica;
- validação da migration em PostgreSQL compatível com produção;
- `dotnet test` e `dotnet build`.

Frontend:

- testes dos hooks e do fluxo de cadastro rápido;
- teste de preservação do formulário e seleção automática;
- TypeScript, lint, testes e build.

Antes de concluir, executar o `software-quality-gate` em cada repositório, com `git status` antes e depois. O scanner não instalará dependências, não alterará manifests ou lockfiles e não deixará arquivos gerados.

## Deploy

Backend e frontend exigem deploy coordenado porque o contrato muda de `tipoSessao` para `tipoSessaoId`. A migration será versionada. A aplicação em produção ocorrerá somente com uma conexão de produção inequívoca; caso contrário, será entregue o comando exato para execução manual.
